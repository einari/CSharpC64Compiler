namespace RoslynC64Compiler.CodeGeneration;

/// <summary>
/// C64 memory map and system constants
/// </summary>
public static class C64Constants
{
    // Memory regions
    public const ushort BasicStart = 0x0801;        // Start of BASIC program area
    public const ushort BasicEnd = 0x9FFF;          // End of BASIC RAM
    public const ushort FreeRamStart = 0x0800;      // Start of free RAM (after zero page and stack)
    
    // Zero page locations commonly used
    public const byte ZP_Temp1 = 0xFB;              // Temp storage 1
    public const byte ZP_Temp2 = 0xFC;              // Temp storage 2
    public const byte ZP_Temp3 = 0xFD;              // Temp storage 3
    public const byte ZP_Temp4 = 0xFE;              // Temp storage 4
    public const byte ZP_Ptr1Lo = 0x22;             // Pointer 1 low byte
    public const byte ZP_Ptr1Hi = 0x23;             // Pointer 1 high byte
    public const byte ZP_Ptr2Lo = 0x24;             // Pointer 2 low byte
    public const byte ZP_Ptr2Hi = 0x25;             // Pointer 2 high byte

    // KERNAL routine addresses
    public const ushort CHROUT = 0xFFD2;            // Output character to current device
    public const ushort CHRIN = 0xFFCF;             // Input character from current device
    public const ushort GETIN = 0xFFE4;             // Get character from keyboard buffer
    public const ushort CLRCH = 0xFFCC;             // Clear I/O channels
    public const ushort PLOT = 0xFFF0;              // Set/read cursor position
    public const ushort RDTIM = 0xFFDE;             // Read system clock
    public const ushort SETTIM = 0xFFDB;            // Set system clock
    public const ushort STOP = 0xFFE1;              // Check STOP key
    public const ushort SCNKEY = 0xFF9F;            // Scan keyboard
    public const ushort CLRSCR = 0xE544;            // Clear screen (not KERNAL, but ROM routine)
    public const ushort SETLFS = 0xFFBA;            // Set logical file parameters
    public const ushort SETNAM = 0xFFBD;            // Set filename
    public const ushort OPEN = 0xFFC0;              // Open file
    public const ushort CLOSE = 0xFFC3;             // Close file
    public const ushort CHKIN = 0xFFC6;             // Set input channel
    public const ushort CHKOUT = 0xFFC9;            // Set output channel
    public const ushort LOAD = 0xFFD5;              // Load file
    public const ushort SAVE = 0xFFD8;              // Save file

    // Screen memory
    public const ushort ScreenRam = 0x0400;         // Default screen RAM location
    public const ushort ColorRam = 0xD800;          // Color RAM
    public const int ScreenWidth = 40;
    public const int ScreenHeight = 25;

    // VIC-II registers
    public const ushort VIC_Base = 0xD000;
    public const ushort VIC_Border = 0xD020;        // Border color
    public const ushort VIC_Background = 0xD021;   // Background color

    // SID registers
    public const ushort SID_Base = 0xD400;

    // CIA registers
    public const ushort CIA1_Base = 0xDC00;
    public const ushort CIA2_Base = 0xDD00;

    // Colors
    public const byte Black = 0;
    public const byte White = 1;
    public const byte Red = 2;
    public const byte Cyan = 3;
    public const byte Purple = 4;
    public const byte Green = 5;
    public const byte Blue = 6;
    public const byte Yellow = 7;
    public const byte Orange = 8;
    public const byte Brown = 9;
    public const byte LightRed = 10;
    public const byte DarkGrey = 11;
    public const byte Grey = 12;
    public const byte LightGreen = 13;
    public const byte LightBlue = 14;
    public const byte LightGrey = 15;

    // PETSCII codes
    public const byte PETSCII_Return = 13;
    public const byte PETSCII_Clear = 147;
    public const byte PETSCII_Home = 19;
    public const byte PETSCII_CursorDown = 17;
    public const byte PETSCII_CursorUp = 145;
    public const byte PETSCII_CursorRight = 29;
    public const byte PETSCII_CursorLeft = 157;
    public const byte PETSCII_ReverseOn = 18;
    public const byte PETSCII_ReverseOff = 146;
}
