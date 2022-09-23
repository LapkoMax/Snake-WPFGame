using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace Snake
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer gameTickTimer = new();
        private readonly Random rnd = new();

        private const int SnakeSquareSize = 20;
        private const int SnakeStartLength = 3;
        private const int SnakeStartSpeed = 400;
        private const int SnakeSpeedThreshold = 100;
        private const int MaxHighscoreListEntryCount = 5;

        private readonly SolidColorBrush snakeBodyBrush = Brushes.Green;
        private readonly SolidColorBrush snakeHeadBrush = Brushes.YellowGreen;
        private readonly SolidColorBrush foodBrush = Brushes.Red;
        private UIElement snakeFood;
        private readonly List<SnakePart> snakeParts = new();

        public ObservableCollection<SnakeHighscore> HighscoreList
        {
            get; set;
        } = new ObservableCollection<SnakeHighscore>();

        public enum SnakeDirection
        {
            Left,
            Right,
            Up,
            Down
        };
        private SnakeDirection snakeDirection = SnakeDirection.Right;
        private int snakeLength;
        private int currentScore;

        public MainWindow()
        {
            InitializeComponent();
            gameTickTimer.Tick += GameTickTimer_Tick;
            LoadHighscoreList();
        }

        private void GameTickTimer_Tick(object sender, EventArgs e)
        {
            MoveSnake();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            SnakeDirection originalDirection = snakeDirection;
            switch (e.Key)
            {
                case Key.Up:
                case Key.W:
                    if (snakeDirection != SnakeDirection.Down)
                    {
                        snakeDirection = SnakeDirection.Up;
                    }
                    break;
                case Key.Down:
                case Key.S:
                    if (snakeDirection != SnakeDirection.Up)
                    {
                        snakeDirection = SnakeDirection.Down;
                    }
                    break;
                case Key.Left:
                case Key.A:
                    if (snakeDirection != SnakeDirection.Right)
                    {
                        snakeDirection = SnakeDirection.Left;
                    }
                    break;
                case Key.Right:
                case Key.D:
                    if (snakeDirection != SnakeDirection.Left)
                    {
                        snakeDirection = SnakeDirection.Right;
                    }
                    break;
                case Key.Space:
                    StartNewGame();
                    break;
                default:
                    break;
            }

            if (snakeDirection != originalDirection)
            {
                MoveSnake();
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            DrawGameArea();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnShowHighscoreList_Click(object sender, RoutedEventArgs e)
        {
            bdrWelcomeMessage.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Visible;
        }

        private void BtnAddToHighscoreList_Click(object sender, RoutedEventArgs e)
        {
            int newIndex = 0;
            if(HighscoreList.Count < MaxHighscoreListEntryCount || currentScore > HighscoreList.Min(x => x.Score))
            {
                SnakeHighscore justAbove = HighscoreList.Count > 0 ? HighscoreList.OrderByDescending(x => x.Score).FirstOrDefault(x => x.Score >= currentScore) : null;
                if(justAbove != null)
                {
                    newIndex = HighscoreList.IndexOf(justAbove) + 1;
                }

                HighscoreList.Insert(newIndex, new SnakeHighscore() 
                {
                    PlayerName = txtPalyerName.Text,
                    Score = currentScore
                });

                while(HighscoreList.Count > MaxHighscoreListEntryCount)
                {
                    HighscoreList.RemoveAt(MaxHighscoreListEntryCount);
                }

                SaveHighscoreList();

                bdrNewHighscore.Visibility = Visibility.Collapsed;
                bdrHighscoreList.Visibility = Visibility.Visible;
            }
        }

        private void StartNewGame()
        {
            bdrWelcomeMessage.Visibility = Visibility.Collapsed;
            bdrHighscoreList.Visibility = Visibility.Collapsed;
            bdrEndOfGame.Visibility = Visibility.Collapsed;

            foreach (SnakePart snakePart in snakeParts)
            {
                if (snakePart.UiElement != null)
                {
                    GameArea.Children.Remove(snakePart.UiElement);
                }
            }
            snakeParts.Clear();
            if (snakeFood != null)
            {
                GameArea.Children.Remove(snakeFood);
            }

            currentScore = 0;
            snakeLength = SnakeStartLength;
            snakeDirection = SnakeDirection.Right;
            snakeParts.Add(new SnakePart()
            {
                Position = new Point(SnakeSquareSize * 5, SnakeSquareSize * 5)
            });
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(SnakeStartSpeed);

            DrawSnake();
            DrawSnakeFood();

            UpdateGameStatus();

            gameTickTimer.IsEnabled = true;
        }

        private void DrawGameArea()
        {
            bool doneDrawingBackground = false;
            int nextX = 0, nextY = 0;
            int rowCounter = 0;
            bool nextIsOdd = false;

            while (!doneDrawingBackground)
            {
                Rectangle rect = new()
                {
                    Width = SnakeSquareSize,
                    Height = SnakeSquareSize,
                    Fill = nextIsOdd ? Brushes.White : Brushes.Black
                };
                GameArea.Children.Add(rect);
                Canvas.SetTop(rect, nextY);
                Canvas.SetLeft(rect, nextX);

                nextIsOdd = !nextIsOdd;
                nextX += SnakeSquareSize;
                if (nextX >= GameArea.ActualWidth)
                {
                    nextX = 0;
                    nextY += SnakeSquareSize;
                    rowCounter++;
                    nextIsOdd = rowCounter % 2 != 0;
                }

                if (nextY >= GameArea.ActualHeight)
                {
                    doneDrawingBackground = true;
                }
            }
        }

        private void DrawSnake()
        {
            foreach (SnakePart snakePart in snakeParts)
            {
                if (snakePart.UiElement == null)
                {
                    snakePart.UiElement = new Rectangle()
                    {
                        Width = SnakeSquareSize,
                        Height = SnakeSquareSize,
                        Fill = snakePart.IsHead ? snakeHeadBrush : snakeBodyBrush
                    };
                    GameArea.Children.Add(snakePart.UiElement);
                    Canvas.SetTop(snakePart.UiElement, snakePart.Position.Y);
                    Canvas.SetLeft(snakePart.UiElement, snakePart.Position.X);
                }
            }
        }

        private void MoveSnake()
        {
            while (snakeParts.Count >= snakeLength)
            {
                GameArea.Children.Remove(snakeParts[0].UiElement);
                snakeParts.RemoveAt(0);
            }

            foreach (SnakePart snakePart in snakeParts)
            {
                (snakePart.UiElement as Rectangle).Fill = snakeBodyBrush;
                snakePart.IsHead = false;
            }

            SnakePart snakeHead = snakeParts[^1];
            double nextX = snakeHead.Position.X;
            double nextY = snakeHead.Position.Y;
            switch (snakeDirection)
            {
                case SnakeDirection.Left:
                    nextX -= SnakeSquareSize;
                    break;
                case SnakeDirection.Right:
                    nextX += SnakeSquareSize;
                    break;
                case SnakeDirection.Up:
                    nextY -= SnakeSquareSize;
                    break;
                case SnakeDirection.Down:
                    nextY += SnakeSquareSize;
                    break;
                default:
                    break;
            }

            snakeParts.Add(new SnakePart()
            {
                Position = new Point(nextX, nextY),
                IsHead = true
            });
            DrawSnake();

            DoCollisionCheck();
        }

        private void DrawSnakeFood()
        {
            Point foodPosition = GetNextFoodPosition();
            snakeFood = new Ellipse()
            {
                Width = SnakeSquareSize,
                Height = SnakeSquareSize,
                Fill = foodBrush
            };
            GameArea.Children.Add(snakeFood);
            Canvas.SetTop(snakeFood, foodPosition.X);
            Canvas.SetLeft(snakeFood, foodPosition.Y);
        }

        private Point GetNextFoodPosition()
        {
            int maxX = (int)(GameArea.ActualWidth / SnakeSquareSize);
            int maxY = (int)(GameArea.ActualHeight / SnakeSquareSize);
            int foodX = rnd.Next(0, maxX) * SnakeSquareSize;
            int foodY = rnd.Next(0, maxY) * SnakeSquareSize;

            foreach (SnakePart snakePart in snakeParts)
            {
                if (snakePart.Position.X == foodX && snakePart.Position.Y == foodY)
                {
                    return GetNextFoodPosition();
                }
            }

            return new Point(foodX, foodY);
        }

        private void DoCollisionCheck()
        {
            SnakePart snakeHead = snakeParts[^1];

            if (snakeHead.Position.X == Canvas.GetLeft(snakeFood) && snakeHead.Position.Y == Canvas.GetTop(snakeFood))
            {
                EatSnakeFood();
                return;
            }

            if (snakeHead.Position.Y < 0 || snakeHead.Position.Y >= GameArea.ActualHeight || snakeHead.Position.X < 0 || snakeHead.Position.X >= GameArea.ActualWidth)
            {
                EndGame();
            }

            foreach (SnakePart snakePart in snakeParts.Take(snakeParts.Count - 1))
            {
                if (snakeHead.Position.X == snakePart.Position.X && snakeHead.Position.Y == snakePart.Position.Y)
                {
                    EndGame();
                }
            }
        }

        private void EatSnakeFood()
        {
            snakeLength++;
            currentScore++;
            int timerInterval = Math.Max(SnakeSpeedThreshold, (int)gameTickTimer.Interval.TotalMilliseconds - (currentScore * 2));
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(timerInterval);
            GameArea.Children.Remove(snakeFood);
            DrawSnakeFood();
            UpdateGameStatus();
        }

        private void UpdateGameStatus()
        {
            tbStatusScore.Text = currentScore.ToString();
            tbStatusSpeed.Text = gameTickTimer.Interval.TotalMilliseconds.ToString();
        }

        private void EndGame()
        {
            bool isNewHighscore = false;
            if(currentScore > 0)
            {
                int lowestHighscore = HighscoreList.Count > 0 ? HighscoreList.Min(x => x.Score) : 0;
                if(currentScore > lowestHighscore || HighscoreList.Count < MaxHighscoreListEntryCount)
                {
                    bdrNewHighscore.Visibility = Visibility.Visible;
                    txtPalyerName.Focus();
                    isNewHighscore = true;
                }
            }
            if (!isNewHighscore)
            {
                tbFinalScore.Text = currentScore.ToString();
                bdrEndOfGame.Visibility = Visibility.Visible;
            }

            gameTickTimer.IsEnabled = false;
        }

        private void LoadHighscoreList()
        {
            if (File.Exists("snake_highscorelist.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<SnakeHighscore>));
                using(Stream reader = new FileStream("snake_highscorelist.xml", FileMode.Open))
                {
                    List<SnakeHighscore> tempList = (List<SnakeHighscore>)serializer.Deserialize(reader);
                    HighscoreList.Clear();
                    foreach(var item in tempList.OrderByDescending(x => x.Score))
                    {
                        HighscoreList.Add(item);
                    }
                }
            }
        }

        private void SaveHighscoreList()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(ObservableCollection<SnakeHighscore>));
            using(Stream writer = new FileStream("snake_highscorelist.xml", FileMode.OpenOrCreate))
            {
                serializer.Serialize(writer, HighscoreList);
            }
        }
    }
}
