using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MonoGame.Extended.Animations.SpriteSheets;
using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.Input.InputListeners;
using MonoGame.Extended.Sprites;
using MonoGame.Extended.TextureAtlases;

namespace SudokuDream
{
    /// <summary>
    /// Sudoku Dream Game - Rough Out
    /// </summary>
    public class SudokuDreamGame : Game
    {
        GraphicsDeviceManager _graphics;
        SpriteBatch _spriteBatch;
        readonly FramesPerSecondCounter _fpsCounter = new FramesPerSecondCounter();
        BitmapFont _bitmapFont;
        BitmapFont _impactFont;
        Texture2D _sudokuTiles;
        Texture2D _youWin;
        Texture2D _youLose;
        const int TILE_WIDTH = 60;
        const int TILE_HEIGHT = 55;
        static int x_offset = 0;
        static int y_offset = 0;
        Point TILE_DIMENSIONS = new Point(TILE_WIDTH, TILE_HEIGHT);
        readonly List<string> _logLines = new List<string>();
        const int _maxLogLines = 13;
        List<BoardTile> _board = new List<BoardTile>();
        Stopwatch _gameTimer = new Stopwatch();
        int _score = 0;
        int _multiplier = 1;
        int _rateOfReduction = 15 * 1000;
        long _lastMultiplierReductionTime =0;

        public enum GameStates { WIN, LOSE, LOADING, PLAYING}

        public GameStates _currentState;

        internal class BoardTile
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Value { get; set; }
            public bool Selected { get; set; }
            public bool Editable { get; set; }

            Rectangle _rectangle;

            public void Init()
            {
                //forces geometry create (optimization
                _rectangle = new Rectangle(X*TILE_WIDTH+x_offset, Y*TILE_HEIGHT+y_offset, TILE_WIDTH, TILE_HEIGHT);

            }

            public Rectangle Rect => _rectangle;

        }

        public SudokuDreamGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferHeight = 768;
            _graphics.PreferredBackBufferWidth = 1024;

            //puzzle size
            var pWidth = TILE_WIDTH*9;
            var pHeight = TILE_HEIGHT * 9;
            //if there's extra
            x_offset = (_graphics.PreferredBackBufferWidth - pWidth) / 2;
            x_offset = x_offset > 0 ? x_offset : 0;
            y_offset = (_graphics.PreferredBackBufferHeight - pHeight) / 2;
            y_offset = y_offset > 0 ? y_offset : 0;

            IsMouseVisible = true;
            Window.AllowUserResizing = true;
            Content.RootDirectory = "Content";
            _currentState = GameStates.PLAYING;
            LogMessage("F1 Check, F5 Play again, ESC Exit");
            
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            var mouseListener = new MouseListener(new MouseListenerSettings());
            Components.Add(new InputListenerComponent(this, mouseListener));

            mouseListener.MouseClicked += (sender, args) => SelectTile(args);
            mouseListener.MouseDoubleClicked += (sender, args) => RotateTile(args);
            mouseListener.MouseDown += (sender, args) => SelectTile(args);

            base.Initialize();
        }

        private void SelectTile(MouseEventArgs mouseArgs)
        {
            if (mouseArgs.Button == MouseButton.Left)
            {
                //determine which cell we're intersecting. 
                var x = (mouseArgs.Position.X - x_offset) / TILE_WIDTH  ;  //yields 0-9
                var y =( mouseArgs.Position.Y - y_offset) / TILE_HEIGHT ;  // yields 0-9                

                foreach (var vt in _board)
                {
                    vt.Selected = false; //unselect all former tiles
                }
                var tile = _board.First(_ => _.X == x && _.Y == y);

                if( tile.Editable )
                    tile.Selected = true;

            }
        }

        private void RotateTile(MouseEventArgs mouseArgs)
        {
            //figure out the tile and see if it's selected
            //determine which cell we're intersecting. 
            var x = (mouseArgs.Position.X -x_offset) / TILE_WIDTH;  //yields 0-9
            var y = (mouseArgs.Position.Y-y_offset) / TILE_HEIGHT;  // yields 0-9
            var tile = _board.Where(_ => _.X == x && _.Y == y).First();
            
            if (tile.Selected)
            {
                if (mouseArgs.Button == MouseButton.Left)
                {
                    tile.Value += 1;
                    if (tile.Value > 10)
                        tile.Value = 1;
                }
                else if (mouseArgs.Button == MouseButton.Right)
                {
                    tile.Value -= 1;
                    if (tile.Value < 1)
                        tile.Value = 9;
                }
            }           

        }

        private List<BoardTile> CreateRandomSudokuBoard()
        {
            
            //circular shift population algorithm
            List<BoardTile> tiles = new List<BoardTile>();
            var rowData = CreateRandomRow();
            Queue<int> r = new Queue<int>(rowData);
            Stack<int>r1 = new Stack<int>();
            int shift = 3;
            for (int i = 0; i < 9; i++)
            {
                //generate row data
                
                int y = 0;
                foreach (var cell in r)
                {                   
                    BoardTile tile = new BoardTile();
                    tile.X = y;
                    tile.Y = i;
                    tile.Value = cell;
                    tile.Selected = false;
                    tile.Init();
                    y++;
                    tiles.Add(tile);
                }
                //shift
               r.Enqueue(r.Dequeue());
               r.Enqueue(r.Dequeue());
               r.Enqueue(r.Dequeue());
               r.Enqueue(r.Dequeue());


            }



            return tiles;
        }

        private List<int> CreateRandomRow()
        {
            List<int> row = new List<int>();
            for (int i = 0; i < 9; i++)
            {
                int u = GenerateNonRepeatingRandom(row);
                row.Add(u);
            }
            return row;
        }

        private int GenerateNonRepeatingRandom(List<int> match)
        {
            Random r=new Random();
            while (true)
            {
                var num = r.Next(1, 10);
                if (!match.Contains(num))
                    return num;
            }
        }

        private bool IsValidSolution()
        {    
            //first see if there are any empty cells
            var hasEmpty = _board.Any(x => x.Value > 9 || x.Value < 1);
            if (hasEmpty)
                return false;

            //check columns
            for (int i = 0; i < 9; i++)
            {
                var columns = _board.Where(_ => _.X == i).ToList();
                var foundVals = new List<int>();
                foreach (var col in columns)
                {
                    if (col.Value > 9 || col.Value < 1)
                        return false; //empty cell
                    if (foundVals.Contains(col.Value))
                        return false; //dupe
                    foundVals.Add(col.Value);
                }
            }
            for (int i = 0; i < 9; i++)
            {
                var rows = _board.Where(_ => _.Y == i).ToList();
                var foundVals = new List<int>();
                foreach (var row in rows)
                {
                    if (row.Value > 9 || row.Value < 1)
                        return false; //empty cell
                    if (foundVals.Contains(row.Value))
                        return false; //dupe
                    foundVals.Add(row.Value);
                }
            }

            return true;
        }



        private void LogMessage(string messageFormat, params object[] args)
        {
            var message = string.Format(messageFormat, args);

            if (_logLines.Count == _maxLogLines)
                _logLines.RemoveAt(0);

            _logLines.Add(message);
        }

        private Rectangle CreateTileRectangle(int tileNum)
        {
            Point newLoc = new Point(TILE_WIDTH * tileNum, 0);
            return new Rectangle(newLoc, TILE_DIMENSIONS);
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _sudokuTiles = Content.Load<Texture2D>("Tiles/numbers");
            _bitmapFont = Content.Load<BitmapFont>("Fonts/montserrat-32");
            _impactFont = Content.Load<BitmapFont>("Fonts/impact-32");
            _youWin = Content.Load<Texture2D>("Textures/win");
            _youLose = Content.Load<Texture2D>("Textures/lose");
            CreateNewGame();
            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        private void CreateNewGame()
        {
            _board= CreateRandomSudokuBoard();

            Random r = new Random();
            //remove tiles
            int i = 55;
            while (i-- > 0)
            {
                var t= r.Next(0, _board.Count);
                _board[t].Value = 10;
                _board[t].Editable = true;
            }
            _score = 0;
            _multiplier = 1;
            _gameTimer.Restart();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            _fpsCounter.Update(gameTime);
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.F1))
            {
                if (_currentState != GameStates.WIN && _currentState != GameStates.LOSE)
                {
                    var isSolved = IsValidSolution();

                    _score = (10000 + (isSolved ? 500 : 0)) -
                             ((_multiplier / 10) * ((int) _gameTimer.ElapsedMilliseconds / 10000));

                    if (isSolved)
                    {
                        _currentState = GameStates.WIN;
                    }
                    else
                    {
                        _currentState = GameStates.LOSE;
                    }
                }
            }

            if (Keyboard.GetState().IsKeyDown(Keys.F5))
            {
                if (_currentState == GameStates.WIN || _currentState == GameStates.LOSE)
                {
                    CreateNewGame();
                    _currentState = GameStates.PLAYING;
                }
            }

            if (_currentState == GameStates.PLAYING)
            {
                _lastMultiplierReductionTime = (long) _gameTimer.ElapsedMilliseconds;
                _score = 10000 -
                         ((_multiplier / 10) * ((int)_gameTimer.ElapsedMilliseconds / 10000));
                if ( _lastMultiplierReductionTime > _rateOfReduction )
                {                    
                    //reduce multipler every interval
                    _lastMultiplierReductionTime = _gameTimer.ElapsedMilliseconds;                   
                    _multiplier += 1;


                }
            }

                base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);


            _spriteBatch.Begin();
            _fpsCounter.Draw(gameTime);
            Window.Title = $"{"FPS"} {_fpsCounter.FramesPerSecond}";

            //draw board
            foreach (var tile in _board)
            {
                if (tile.Selected)
                {
                    _spriteBatch.Draw(_sudokuTiles, tile.Rect, CreateTileRectangle(tile.Value), Color.Yellow);
                }
                else if (tile.Editable)
                {
                    _spriteBatch.Draw(_sudokuTiles, tile.Rect, CreateTileRectangle(tile.Value), Color.Beige);
                }
                else
                {
                    _spriteBatch.Draw(_sudokuTiles, tile.Rect, CreateTileRectangle(tile.Value), Color.White);
                }
            }

            for (var i = 0; i < _logLines.Count; i++)
            {
                var logLine = _logLines[i];
                _spriteBatch.DrawString(_bitmapFont, logLine, new Vector2(4, i * _bitmapFont.LineHeight), Color.Red * 0.2f);
            }

            if (_currentState == GameStates.PLAYING)
            {
                _spriteBatch.DrawString(_impactFont, _gameTimer.Elapsed.ToString("mm\\:ss"),
                    new Vector2((Window.ClientBounds.Width / 4) * 3, 0), Color.Gray);

                _spriteBatch.DrawString(_impactFont, "Score: " + _score.ToString(), new Vector2((Window.ClientBounds.Width / 4) * 3, 30), Color.White);
            }

            if (_currentState == GameStates.WIN || _currentState == GameStates.LOSE)
            {                
                _spriteBatch.DrawString(_impactFont, "Score: " + _score.ToString(), new Vector2((Window.ClientBounds.Width / 4) * 3, 5), Color.White);
                
            }
            if( _currentState == GameStates.WIN)
                _spriteBatch.Draw(_youWin, new Vector2((Window.ClientBounds.Width-_youWin.Width)/2, Window.ClientBounds.Height/3), Color.White);
            if(_currentState == GameStates.LOSE)
                _spriteBatch.Draw(_youLose, new Vector2((Window.ClientBounds.Width - _youWin.Width) / 2, Window.ClientBounds.Height / 3), Color.White);            

            _spriteBatch.End();

            // TODO: Add your drawing code here

            base.Draw(gameTime);
        }
    }
}
