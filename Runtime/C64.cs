// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
#pragma warning disable CA1822 // Mark members as static

namespace RoslynC64Compiler.Runtime;

/// <summary>
/// C64 intrinsic functions - these are recognized by the compiler
/// and generate direct 6502 code, not actual C# method calls.
/// 
/// Include this file in your C# project when developing C64 programs
/// to get IntelliSense support and avoid compilation errors.
/// </summary>
public static class C64
{
    // === Screen and Text ===
    
    /// <summary>
    /// Print a string to the screen
    /// </summary>
    public static void Print(string text) { }
    
    /// <summary>
    /// Print a string followed by a newline
    /// </summary>
    public static void PrintLine(string text) { }
    
    /// <summary>
    /// Print a single character (PETSCII code)
    /// </summary>
    public static void PrintChar(byte character) { }
    
    /// <summary>
    /// Clear the screen
    /// </summary>
    public static void ClearScreen() { }
    
    /// <summary>
    /// Exit the program and return to BASIC prompt
    /// </summary>
    public static void Exit() { }
    
    // === Input ===
    
    /// <summary>
    /// Wait for and return a keypress
    /// </summary>
    public static byte GetKey() => 0;
    
    // === Colors ===
    
    /// <summary>
    /// Set the border color (0-15)
    /// </summary>
    public static void SetBorderColor(byte color) { }
    
    /// <summary>
    /// Set the background color (0-15)
    /// </summary>
    public static void SetBackgroundColor(byte color) { }
    
    /// <summary>
    /// Set the text cursor color (0-15)
    /// </summary>
    public static void SetTextColor(byte color) { }
    
    // === Memory Access ===
    
    /// <summary>
    /// Write a byte to memory
    /// </summary>
    public static void Poke(ushort address, byte value) { }
    
    /// <summary>
    /// Read a byte from memory
    /// </summary>
    public static byte Peek(ushort address) => 0;
    
    /// <summary>
    /// Write a 16-bit value to memory (low byte first)
    /// </summary>
    public static void Poke16(ushort address, ushort value) { }
    
    /// <summary>
    /// Read a 16-bit value from memory (low byte first)
    /// </summary>
    public static ushort Peek16(ushort address) => 0;
    
    // === Screen RAM ===
    
    /// <summary>
    /// Write a character to screen RAM at position (x, y)
    /// </summary>
    public static void PlotChar(byte x, byte y, byte character) { }
    
    /// <summary>
    /// Set the color at position (x, y) in color RAM
    /// </summary>
    public static void PlotColor(byte x, byte y, byte color) { }
    
    /// <summary>
    /// Move cursor to position (x, y)
    /// </summary>
    public static void SetCursor(byte x, byte y) { }
    
    // === Timing ===
    
    /// <summary>
    /// Simple delay loop
    /// </summary>
    public static void Delay(byte count) { }
    
    /// <summary>
    /// Read the jiffy clock (1/60th second ticks)
    /// </summary>
    public static uint GetTime() => 0;
    
    // === Sound (SID) ===
    
    /// <summary>
    /// Set SID register
    /// </summary>
    public static void SidWrite(byte register, byte value) { }
    
    /// <summary>
    /// Play a simple tone on voice 1
    /// </summary>
    public static void PlayTone(ushort frequency, byte duration) { }
    
    // === Sprites ===
    
    /// <summary>
    /// Enable/disable a sprite (0-7)
    /// </summary>
    public static void SpriteEnable(byte sprite, bool enable) { }
    
    /// <summary>
    /// Set sprite position
    /// </summary>
    public static void SpritePosition(byte sprite, ushort x, byte y) { }
    
    /// <summary>
    /// Set sprite color
    /// </summary>
    public static void SpriteColor(byte sprite, byte color) { }
    
    /// <summary>
    /// Set sprite data pointer
    /// </summary>
    public static void SpritePointer(byte sprite, byte block) { }
    
    // === Constants ===
    
    public static class Colors
    {
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
    }
    
    public static class Keys
    {
        public const byte Return = 13;
        public const byte Space = 32;
        public const byte CursorUp = 145;
        public const byte CursorDown = 17;
        public const byte CursorLeft = 157;
        public const byte CursorRight = 29;
        public const byte Delete = 20;
        public const byte Insert = 148;
        public const byte Home = 19;
        public const byte Clear = 147;
        public const byte F1 = 133;
        public const byte F3 = 134;
        public const byte F5 = 135;
        public const byte F7 = 136;
        public const byte F2 = 137;
        public const byte F4 = 138;
        public const byte F6 = 139;
        public const byte F8 = 140;
    }
    
    public static class Addresses
    {
        public const ushort ScreenRam = 0x0400;
        public const ushort ColorRam = 0xD800;
        public const ushort BorderColor = 0xD020;
        public const ushort BackgroundColor = 0xD021;
        public const ushort SpriteEnable = 0xD015;
        public const ushort SidBase = 0xD400;
        public const ushort VicBase = 0xD000;
    }
}

#pragma warning restore CA1822
