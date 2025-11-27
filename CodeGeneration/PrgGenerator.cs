namespace RoslynC64Compiler.CodeGeneration;

/// <summary>
/// Generates a C64 PRG file from machine code
/// </summary>
public class PrgGenerator
{
    /// <summary>
    /// Generate a PRG file with the standard load address header
    /// </summary>
    /// <param name="machineCode">The machine code bytes</param>
    /// <param name="loadAddress">The load address (default: $0801 for BASIC area)</param>
    /// <returns>PRG file bytes including 2-byte load address header</returns>
    public byte[] Generate(byte[] machineCode, ushort loadAddress = C64Constants.BasicStart)
    {
        // PRG format: 2-byte little-endian load address followed by program bytes
        var prg = new byte[machineCode.Length + 2];
        
        // Load address header (little-endian)
        prg[0] = (byte)(loadAddress & 0xFF);
        prg[1] = (byte)(loadAddress >> 8);
        
        // Copy machine code
        Array.Copy(machineCode, 0, prg, 2, machineCode.Length);
        
        return prg;
    }

    /// <summary>
    /// Save PRG file to disk
    /// </summary>
    public void Save(string path, byte[] machineCode, ushort loadAddress = C64Constants.BasicStart)
    {
        var prg = Generate(machineCode, loadAddress);
        File.WriteAllBytes(path, prg);
    }

    /// <summary>
    /// Generate a D64 disk image containing the PRG file
    /// </summary>
    public byte[] GenerateD64(byte[] prgData, string programName = "PROGRAM")
    {
        // D64 format constants
        const int TracksCount = 35;
        const int SectorsTrack1_17 = 21;
        const int SectorsTrack18_24 = 19;
        const int SectorsTrack25_30 = 18;
        const int SectorsTrack31_35 = 17;
        const int SectorSize = 256;
        const int D64Size = 174848; // 35 tracks

        var d64 = new byte[D64Size];

        // Initialize all sectors as unused (fill with 0x00)
        // BAM (Block Availability Map) is at track 18, sector 0
        
        // Calculate sector offset for track 18, sector 0 (BAM)
        int bamOffset = GetSectorOffset(18, 0);
        
        // Initialize BAM
        d64[bamOffset + 0] = 18;  // Track of first directory sector
        d64[bamOffset + 1] = 1;   // Sector of first directory sector
        d64[bamOffset + 2] = 0x41; // DOS version type 'A'
        d64[bamOffset + 3] = 0x00; // Unused

        // BAM entries for tracks 1-35 (4 bytes each)
        for (int track = 1; track <= 35; track++)
        {
            int sectorsInTrack = GetSectorsInTrack(track);
            int bamEntryOffset = bamOffset + 4 + (track - 1) * 4;
            
            // Free sectors count
            d64[bamEntryOffset] = (byte)(track == 18 ? sectorsInTrack - 2 : sectorsInTrack); // Reserve BAM and dir
            
            // Bitmap (1 = free, 0 = used)
            uint bitmap = (1u << sectorsInTrack) - 1;
            if (track == 18)
            {
                bitmap &= ~(1u << 0); // BAM used
                bitmap &= ~(1u << 1); // Directory used
            }
            d64[bamEntryOffset + 1] = (byte)(bitmap & 0xFF);
            d64[bamEntryOffset + 2] = (byte)((bitmap >> 8) & 0xFF);
            d64[bamEntryOffset + 3] = (byte)((bitmap >> 16) & 0xFF);
        }

        // Disk name (offset $90, 16 bytes) - pad with $A0 (shifted space)
        var diskName = "C64PROGRAM";
        for (int i = 0; i < 16; i++)
        {
            d64[bamOffset + 0x90 + i] = i < diskName.Length ? (byte)diskName[i] : (byte)0xA0;
        }

        // Disk ID (offset $A2, 2 bytes)
        d64[bamOffset + 0xA2] = (byte)'0';
        d64[bamOffset + 0xA3] = (byte)'0';

        // DOS type (offset $A5, 2 bytes)
        d64[bamOffset + 0xA5] = (byte)'2';
        d64[bamOffset + 0xA6] = (byte)'A';

        // Directory at track 18, sector 1
        int dirOffset = GetSectorOffset(18, 1);
        
        // First two bytes: next track/sector (0/255 = last sector)
        d64[dirOffset + 0] = 0;
        d64[dirOffset + 1] = 0xFF;

        // First directory entry at offset 2
        int entryOffset = dirOffset + 2;
        
        // File type: PRG (0x82 = PRG, closed)
        d64[entryOffset + 0] = 0x82;
        
        // First track/sector of file
        int fileTrack = 1;
        int fileSector = 0;
        d64[entryOffset + 1] = (byte)fileTrack;
        d64[entryOffset + 2] = (byte)fileSector;

        // Filename (16 bytes, padded with $A0)
        programName = programName.ToUpperInvariant();
        if (programName.Length > 16) programName = programName.Substring(0, 16);
        for (int i = 0; i < 16; i++)
        {
            d64[entryOffset + 3 + i] = i < programName.Length ? (byte)programName[i] : (byte)0xA0;
        }

        // File size in sectors
        int fileSectors = (prgData.Length + 253) / 254; // 254 bytes per sector (2 for chain)
        d64[entryOffset + 0x1C] = (byte)(fileSectors & 0xFF);
        d64[entryOffset + 0x1D] = (byte)(fileSectors >> 8);

        // Write file data starting at track 1, sector 0
        int dataOffset = 0;
        int currentTrack = fileTrack;
        int currentSector = fileSector;

        while (dataOffset < prgData.Length)
        {
            int sectorOffset = GetSectorOffset(currentTrack, currentSector);
            int bytesToWrite = Math.Min(254, prgData.Length - dataOffset);
            
            // Mark sector as used in BAM
            MarkSectorUsed(d64, bamOffset, currentTrack, currentSector);

            if (dataOffset + bytesToWrite >= prgData.Length)
            {
                // Last sector
                d64[sectorOffset + 0] = 0;
                d64[sectorOffset + 1] = (byte)(bytesToWrite + 1);
            }
            else
            {
                // More sectors to come - find next free sector
                var (nextTrack, nextSector) = FindNextFreeSector(d64, bamOffset, currentTrack, currentSector);
                d64[sectorOffset + 0] = (byte)nextTrack;
                d64[sectorOffset + 1] = (byte)nextSector;
                currentTrack = nextTrack;
                currentSector = nextSector;
            }

            // Write data
            Array.Copy(prgData, dataOffset, d64, sectorOffset + 2, bytesToWrite);
            dataOffset += bytesToWrite;
        }

        return d64;
    }

    private static int GetSectorOffset(int track, int sector)
    {
        int offset = 0;
        for (int t = 1; t < track; t++)
        {
            offset += GetSectorsInTrack(t) * 256;
        }
        offset += sector * 256;
        return offset;
    }

    private static int GetSectorsInTrack(int track)
    {
        if (track <= 17) return 21;
        if (track <= 24) return 19;
        if (track <= 30) return 18;
        return 17;
    }

    private static void MarkSectorUsed(byte[] d64, int bamOffset, int track, int sector)
    {
        int bamEntryOffset = bamOffset + 4 + (track - 1) * 4;
        d64[bamEntryOffset]--; // Decrease free count
        
        int byteIndex = 1 + sector / 8;
        int bitIndex = sector % 8;
        d64[bamEntryOffset + byteIndex] &= (byte)~(1 << bitIndex);
    }

    private static (int track, int sector) FindNextFreeSector(byte[] d64, int bamOffset, int currentTrack, int currentSector)
    {
        // Simple allocation: try next sector, then next track
        int track = currentTrack;
        int sector = currentSector + 1;

        while (track <= 35)
        {
            if (track == 18)
            {
                track++;
                sector = 0;
                continue;
            }

            int sectorsInTrack = GetSectorsInTrack(track);
            if (sector >= sectorsInTrack)
            {
                track++;
                sector = 0;
                continue;
            }

            // Check if sector is free
            int bamEntryOffset = bamOffset + 4 + (track - 1) * 4;
            int byteIndex = 1 + sector / 8;
            int bitIndex = sector % 8;
            
            if ((d64[bamEntryOffset + byteIndex] & (1 << bitIndex)) != 0)
            {
                return (track, sector);
            }

            sector++;
        }

        throw new InvalidOperationException("Disk full");
    }
}
