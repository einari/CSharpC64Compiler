// Memory Poke Demo for Commodore 64
// Direct memory access to screen RAM

class PokeDemo
{
    static void Main()
    {
        C64.ClearScreen();
        
        // Screen RAM starts at $0400 (1024)
        // Color RAM starts at $D800 (55296)
        
        ushort screenBase = 1024;
        ushort colorBase = 55296;
        
        byte x = 0;
        
        // Fill first line with characters
        while (x < 40)
        {
            // Write character to screen RAM
            C64.Poke((ushort)(screenBase + x), (byte)(65 + (x & 15)));  // A-P pattern
            
            // Write color to color RAM
            C64.Poke((ushort)(colorBase + x), (byte)(x & 15));  // Cycle through colors
            
            x++;
        }
        
        // Write message at specific position
        // Row 12 (middle of screen)
        ushort pos = (ushort)(screenBase + 12 * 40 + 10);
        
        // Write "HELLO" manually
        C64.Poke(pos, 8);       // H
        C64.Poke((ushort)(pos + 1), 5);   // E
        C64.Poke((ushort)(pos + 2), 12);  // L
        C64.Poke((ushort)(pos + 3), 12);  // L
        C64.Poke((ushort)(pos + 4), 15);  // O
        
        // Set colors for HELLO (yellow)
        ushort colorPos = (ushort)(colorBase + 12 * 40 + 10);
        C64.Poke(colorPos, 7);
        C64.Poke((ushort)(colorPos + 1), 7);
        C64.Poke((ushort)(colorPos + 2), 7);
        C64.Poke((ushort)(colorPos + 3), 7);
        C64.Poke((ushort)(colorPos + 4), 7);
        
        // Wait for key
        C64.GetKey();
    }
}
