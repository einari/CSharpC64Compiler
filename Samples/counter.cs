// Counter Demo for Commodore 64
// Shows variables, loops, and arithmetic

class Program
{
    static byte counter;
    static byte limit;
    
    static void Main()
    {
        C64.ClearScreen();
        
        Console.WriteLine("COUNTING DEMO");
        Console.WriteLine("");
        
        counter = 0;
        limit = 10;
        
        // Count up
        Console.WriteLine("COUNTING UP:");
        while (counter < limit)
        {
            // Print counter value (as character)
            C64.PrintChar((byte)(48 + counter));  // 48 = '0' in PETSCII
            C64.PrintChar(32);  // Space
            counter++;
        }
        
        Console.WriteLine("");
        Console.WriteLine("");
        
        // Count down
        Console.WriteLine("COUNTING DOWN:");
        while (counter > 0)
        {
            counter--;
            C64.PrintChar((byte)(48 + counter));
            C64.PrintChar(32);
        }
        
        Console.WriteLine("");
        Console.WriteLine("");
        Console.WriteLine("DONE!");
        Console.WriteLine("Press any key to exit...");
        
        C64.GetKey();
        C64.Exit();
    }
}
