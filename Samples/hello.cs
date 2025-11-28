// Hello World for Commodore 64
// Compile with: RoslynC64Compiler hello.cs

// Simple "Hello World" - this compiles to a C64 PRG file!

// Change colors: blue background, light blue border

C64.ClearScreen();
C64.Poke(0xd020, 0);  // Border = light blue
C64.Poke(0xd021, 0);   // Background = blue

Console.WriteLine("HELLO, COMMODORE 64!");
Console.WriteLine("THIS WAS WRITTEN IN C#");
Console.WriteLine("");
Console.WriteLine("PRESS ANY KEY...");

// Wait for keypress (maps to KERNAL GETIN)
Console.ReadKey();
