// Simple Game Loop for Commodore 64
// Basic interactive program structure

class Game
{
    static byte playerX;
    static byte playerY;
    static bool running;
    
    static void Main()
    {
        C64.ClearScreen();
        C64.SetBorderColor(0);  // Black border
        C64.SetBackgroundColor(0);  // Black background
        
        Console.WriteLine("SIMPLE GAME");
        Console.WriteLine("");
        Console.WriteLine("USE KEYS TO MOVE:");
        Console.WriteLine("  W = UP");
        Console.WriteLine("  S = DOWN");  
        Console.WriteLine("  A = LEFT");
        Console.WriteLine("  D = RIGHT");
        Console.WriteLine("  Q = QUIT");
        Console.WriteLine("");
        Console.WriteLine("PRESS ANY KEY TO START");
        
        C64.GetKey();
        
        C64.ClearScreen();
        
        // Initialize player position
        playerX = 20;
        playerY = 12;
        running = true;
        
        // Draw initial player
        DrawPlayer();
        
        // Game loop
        while (running)
        {
            byte key = C64.GetKey();
            
            // Clear old position
            ClearPlayer();
            
            // Handle input
            if (key == 87 || key == 119)  // W or w
            {
                if (playerY > 0)
                    playerY--;
            }
            if (key == 83 || key == 115)  // S or s
            {
                if (playerY < 24)
                    playerY++;
            }
            if (key == 65 || key == 97)   // A or a
            {
                if (playerX > 0)
                    playerX--;
            }
            if (key == 68 || key == 100)  // D or d
            {
                if (playerX < 39)
                    playerX++;
            }
            if (key == 81 || key == 113)  // Q or q
            {
                running = false;
            }
            
            // Draw new position
            DrawPlayer();
        }
        
        // Game over
        C64.ClearScreen();
        Console.WriteLine("GAME OVER");
        Console.WriteLine("THANKS FOR PLAYING!");
        C64.Exit();
    }
    
    static void DrawPlayer()
    {
        ushort pos = (ushort)(1024 + playerY * 40 + playerX);
        ushort colorPos = (ushort)(55296 + playerY * 40 + playerX);
        
        C64.Poke(pos, 81);        // Filled circle character
        C64.Poke(colorPos, 1);    // White color
    }
    
    static void ClearPlayer()
    {
        ushort pos = (ushort)(1024 + playerY * 40 + playerX);
        C64.Poke(pos, 32);        // Space character
    }
}
