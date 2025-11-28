// Color Demo for Commodore 64
// Demonstrates C64 color manipulation using C# syntax

// Clear the screen first
C64.ClearScreen();

// Set border and background colors
C64.SetBorderColor(C64.Colors.Blue);
C64.SetBackgroundColor(C64.Colors.LightBlue);

Console.WriteLine("C64 COLOR DEMO");
Console.WriteLine("");

// Cycle through all 16 colors on the border
byte color = 0;
while (color < 16)
{
    C64.SetBorderColor(color);
    
    // Simple delay
    byte delay = 0;
    while (delay < 255)
    {
        delay++;
    }
    delay = 0;
    while (delay < 255)
    {
        delay++;
    }
    
    color++;
}

// Reset to default colors
C64.SetBorderColor(C64.Colors.LightBlue);

Console.WriteLine("DONE! PRESS ANY KEY");
C64.GetKey();
C64.Exit();