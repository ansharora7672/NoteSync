using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WebSocketSharp;
//using static System.Net.Mime.MediaTypeNames;


namespace PROJ_4_FINAL_GUI
{

    /*public class TextBoxAdorner : Adorner
    {
        private TextBox textBox;
        private Point startPoint;
        private bool isDragging;

        public TextBoxAdorner(UIElement adornedElement) : base(adornedElement)
        {
            textBox = new TextBox
            {
                Width = 100,
                Height = 20,
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                TextWrapping = TextWrapping.Wrap
            };
            AddVisualChild(textBox);
            AddLogicalChild(textBox);

            
            var adornedElementRect = new Rect(adornedElement.RenderSize);
            var x = adornedElementRect.Width / 2 - textBox.Width / 2;
            var y = adornedElementRect.Height / 2 - textBox.Height / 2;
            Canvas.SetLeft(textBox, x);
            Canvas.SetTop(textBox, y);

            textBox.Focus();
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            return textBox;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            startPoint = e.GetPosition(this);
            CaptureMouse();
            isDragging = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            ReleaseMouseCapture();
            isDragging = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsMouseCaptured && isDragging)
            {
                var endPoint = e.GetPosition(this);
                var left = Math.Min(startPoint.X, endPoint.X);
                var top = Math.Min(startPoint.Y, endPoint.Y);
                var width = Math.Abs(startPoint.X - endPoint.X);
                var height = Math.Abs(startPoint.Y - endPoint.Y);

                textBox.Width = width;
                textBox.Height = height;
                Canvas.SetLeft(textBox, left);
                Canvas.SetTop(textBox, top);
            }
        }
    }*/



    public partial class MainWindow : Window
    {
        private string selectedNotebookPath = "";
        private string selectedCanvasFilePath = "";
        private InkCanvas inkCanvas;
        private TextBox _textBox = null;
        private WebSocket ws;
        private bool sendCanvasActionsToServer = false;
        private bool connectedToServer = false;
        private bool isAddingTextBox = false;
        private Point textBoxStartPoint;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWebSocket();
            //Console.WriteLine("console printing working");
            inkCanvas = new InkCanvas();
            PreviewKeyDown += Window_PreviewKeyDown;

        }
        
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {

            HelpWindow contactWindow = new HelpWindow();
            contactWindow.ShowDialog();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            CloseConnection();
        }

        private void InitializeWebSocket()
        {
            ws = new WebSocket("ws://127.0.0.1:7890/EchoAll");
            ws.OnMessage += Ws_OnMessage;

            ws.Connect();

            Packet packet = new Packet(PacketType.EstablishConnection, 1, DateTime.Now, null);

            var serializedPacket = packet.Serialize();
            if (ws.IsAlive)
            {

                ws.Send(serializedPacket);
            }
            else
            {
                CodeTextBlock.Text = "The Server is Down!";
            }

        }

        private void CloseConnection()
        {

            Packet closingPacket = new Packet(PacketType.CloseConnection, 1, DateTime.Now, "Closing connection");
            string serializedPacket = closingPacket.Serialize();
            if (connectedToServer && ws.IsAlive)
            {
                ws.Send(serializedPacket);
            }


            if (ws != null && ws.IsAlive)
            {
                ws.Close();
            }

            sendCanvasActionsToServer = false;
            connectedToServer = false;
        }

        private void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {

                Debug.WriteLine("got a packet!");
                dynamic jsonPacket = JsonConvert.DeserializeObject(e.Data);

                PacketType type = (PacketType)jsonPacket.PacketHeader.Type;
                int sequenceNumber = jsonPacket.PacketHeader.SequenceNumber;
                DateTime timeStamp = jsonPacket.PacketHeader.TimeStamp;

                
                dynamic bodyData = jsonPacket.PacketBody.Data;

                switch (type)
                {
                    case PacketType.DrawingData:
                        
                        HandleDrawingDataPacket(sequenceNumber, timeStamp, bodyData);
                        break;
                    case PacketType.AddImage:
                        
                        HandleAddImagePacket(sequenceNumber, timeStamp, bodyData);
                        break;
                    case PacketType.AddText:
                        
                        HandleAddTextPacket(sequenceNumber, timeStamp, bodyData);
                        break;
                    
                    case PacketType.SessionDetails:
                        HandleSessionDetailsPacket(sequenceNumber, timeStamp, bodyData);
                        break;
                    case PacketType.EstablishConnection:
                        HandleEstablishConnectionPacket(sequenceNumber, timeStamp, bodyData);
                        break;
                    case PacketType.CloseConnection:
                        HandleCloseConnectionPacket(sequenceNumber, timeStamp, bodyData);
                        break;
                    default:
                        Console.WriteLine("Unknown packet type received.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error processing packet: " + ex.Message);
            }
        }

        private void HandleCloseConnectionPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            sendCanvasActionsToServer = false;
            connectedToServer= false;

            ws.Close();
        }

        private void HandleDrawingDataPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            var strokesData = new List<List<Point>>();

            if (bodyData is JArray strokesArray)
            {
                foreach (var strokePoints in strokesArray)
                {
                    var points = new List<Point>();

                    foreach (var pointToken in strokePoints)
                    {
                        var pointString = pointToken.ToString();
                        var pointParts = pointString.Split(',');
                        if (pointParts.Length == 2 && double.TryParse(pointParts[0], out double x) && double.TryParse(pointParts[1], out double y))
                        {
                            points.Add(new Point(x, y));
                        }
                    }
                    strokesData.Add(points);
                }
            }
            else
            {
                Debug.WriteLine("Invalid data format for DrawingData packet.");
                return; 
            }

            Packet receivedPacket = new Packet(PacketType.DrawingData, sequenceNumber, timeStamp, strokesData);

            var strokesDatapacketbody = (List<List<Point>>)receivedPacket.PacketBody.Data;

            write_strokes_data_to_canvas(strokesDatapacketbody);
           
        }


        private void HandleEstablishConnectionPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {

            Packet receivedPacket = new Packet(PacketType.SessionDetails, sequenceNumber, timeStamp, bodyData);

            string received_string = receivedPacket.PacketBody.Data.ToString();

            if (received_string == "Connection Open")
            {

                connectedToServer= true;
            }


        }
        private void HandleAddImagePacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            Debug.WriteLine("inside add image packet");
            try
            {


                        // Extract position and image data from the packet body data
                        double xPos = bodyData.Position.X;
                        double yPos = bodyData.Position.Y;
                        byte[] imageDataBytes = bodyData.ImageData;

                        StaThreadWrapper(() =>
                        {
                            inkCanvas.Dispatcher.Invoke(() =>
                            {
                                
                                Image img = new Image();
                            img.Source = null; 


                            BitmapImage bitmapImage = new BitmapImage();
                            using (MemoryStream stream = new MemoryStream(imageDataBytes))
                            {
                                stream.Position = 0;
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.StreamSource = stream;
                                bitmapImage.EndInit();
                            }

                            bitmapImage.Freeze();

                            // Set the source of the Image control on the UI thread
                            //img.Dispatcher.Invoke(() =>
                            //{
                            img.Source = bitmapImage;

                            //});


                            Image img_forthisthread = new Image();
                            img_forthisthread = img;
                            
                            InkCanvas.SetLeft(img_forthisthread, xPos);
                            InkCanvas.SetTop(img_forthisthread, yPos);
                            inkCanvas.Children.Add(img_forthisthread);



                        });
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error handling AddImage packet: " + ex.Message);
 
            }
        }

        private void StaThreadWrapper(Action action)
        {
            var t = new Thread(o =>
            {
                action();
                System.Windows.Threading.Dispatcher.Run();
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }







        private void HandleAddTextPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            try
            {
                
                string text = bodyData.Text;
                double posX = bodyData.Position.X;
                double posY = bodyData.Position.Y;

                
                TextBox textBox = new TextBox
                {
                    Width = double.NaN,
                    Height = double.NaN,
                    Background = new SolidColorBrush(Colors.Transparent),
                    Foreground = new SolidColorBrush(Colors.Black), 
                    BorderThickness = new Thickness(0),
                    TextWrapping = TextWrapping.Wrap,
                    Text = text 
                };

                
                InkCanvas.SetLeft(textBox, posX);
                InkCanvas.SetTop(textBox, posY);

                
                inkCanvas.Children.Add(textBox);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error handling AddText packet: " + ex.Message);
                
            }
        }


        private void HandleSessionDetailsPacket(int sequenceNumber, DateTime timeStamp, dynamic bodyData)
        {
            Debug.WriteLine("Inside HandleSessionDetailsPacket");
            Packet receivedPacket = new Packet(PacketType.SessionDetails, sequenceNumber, timeStamp, bodyData);

            string received_string = receivedPacket.PacketBody.Data.ToString();
            

            Debug.WriteLine("Session Details Received: " + received_string);
            int number;
            if (received_string.Length == 6 && int.TryParse(received_string, out number))
            {
                sendCanvasActionsToServer = true;
                
                try
                {

                    
                    Dispatcher.Invoke(() =>
                    {
                        CodeTextBlock.Text = "Session Invitation Code: " + received_string;
                    });
                }
                catch (Exception ex)
                {
                    
                    Debug.WriteLine("Error occurred while changing text: " + ex.Message);
                }
            }
            else
            {
                if (received_string == "You have been Successfully added to the Session!")
                {
                    sendCanvasActionsToServer = true;
                }

                try
                {
                    
                    Dispatcher.Invoke(() =>
                    {
                        CodeTextBlock.Text = received_string;
                    });
                }
                catch (Exception ex)
                {
                    
                    Debug.WriteLine("Error occurred while changing text: " + ex.Message);
                }
            }

        }

        /*        private void Window_KeyDown(object sender, KeyEventArgs e)
                {
                    Debug.WriteLine("keydown called");
                    *//*            if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                                {
                                    e.Handled = true;
                                    UndoLastAction();
                                }*//*
                    if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        e.Handled = true;
                        Debug.WriteLine("ctrl v pressed ");
                        PasteImageFromClipboard();
                        Debug.WriteLine("paste image succesfully executed ");

                    }*/
        /*            else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl && e.Key != Key.Enter && e.Key != Key.Back)
                    {
                        CreateOrFocusTextBox(e);


                    }
                }*/
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Debug.WriteLine("preview keydown called");
            if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                Debug.WriteLine("Ctrl+V pressed");
                PasteImageFromClipboard();
                Debug.WriteLine("paste image succesfully executed ");
            }

            if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                Debug.WriteLine("Ctrl+S pressed");
                SaveButton();
                Debug.WriteLine("Canvas Saves Successfully");
            }


        }




        private void PasteImageFromClipboard()
        {
            if (Clipboard.ContainsImage())
            {
                BitmapSource image = Clipboard.GetImage();
                Image img = new Image { Source = image };
                Point position = Mouse.GetPosition(inkCanvas);
                inkCanvas.Children.Add(img); 
                Debug.WriteLine("clipboard contains image");
                InkCanvas.SetLeft(img, position.X);
                InkCanvas.SetTop(img, position.Y);
                //_elementsUndoStack.Push(img);
                SendAddImagePacket(img, position);
            }
            
        }

        private void SendAddImagePacket(Image image, Point position)
        {
            try
            {
                
                byte[] imageDataBytes;
                BitmapSource bitmapSource = (BitmapSource)image.Source;
                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    imageDataBytes = ms.ToArray();
                }

                
                var imageData = new
                {
                    Position = new { X = position.X, Y = position.Y },
                    ImageData = imageDataBytes,
                    
                };

                
                Packet packet = new Packet(PacketType.AddImage, 1, DateTime.Now, imageData);

                
                string serializedPacket = packet.Serialize();

                
                if (connectedToServer && ws.IsAlive && sendCanvasActionsToServer)
                {
                    ws.Send(serializedPacket);
                }
                else
                {
                    CodeTextBlock.Text = "You are not connected to the Server!";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error sending AddImage packet: " + ex.Message);
                
            }
        }




/*        private void CreateOrFocusTextBox(KeyEventArgs e)
        {
            Point position = Mouse.GetPosition(inkCanvas);
            if (_textBox == null)
            {
                _textBox = new TextBox
                {
                    Width = double.NaN,
                    Height = double.NaN,
                    Background = new SolidColorBrush(Colors.Transparent),
                    Foreground = new SolidColorBrush(Colors.Black),
                    BorderThickness = new Thickness(0),
                    TextWrapping = TextWrapping.Wrap
                };
                InkCanvas.SetLeft(_textBox, position.X);
                InkCanvas.SetTop(_textBox, position.Y);
                inkCanvas.Children.Add(_textBox);

                //_elementsUndoStack.Push(_textBox);
            }
            _textBox.Focus();
            if (!char.IsControl((char)e.Key) && e.Key != Key.Enter && e.Key != Key.Back)
            {
                _textBox.Text += e.Key.ToString();
            }
            SendTextPacket(_textBox.Text, position);
        }

        private void SendTextPacket(string text, Point position)
        {
            try
            {
                
                Dictionary<string, object> bodyData = new Dictionary<string, object>();
                bodyData.Add("Text", text);
                bodyData.Add("Position", position);

                
                Packet packet = new Packet(PacketType.AddText, 1, DateTime.Now, bodyData);

                
                var serializedPacket = packet.Serialize();

                
                if (connectedToServer && ws.IsAlive)
                {
                    ws.Send(serializedPacket);
                }
                else
                {
                    CodeTextBlock.Text = "You are not connected to the Server!";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error sending text packet: " + ex.Message);
                
            }
        }


        private void RemoveTextBox()
        {
            if (_textBox != null)
            {
                inkCanvas.Children.Remove(_textBox);
                _textBox = null;
            }
        }*/



        private void write_strokes_data_to_canvas(List<List<Point>> strokesData)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    inkCanvas.Strokes.Clear();
                    foreach (List<Point> strokePoints in strokesData)
                    {
                        StylusPointCollection points = new StylusPointCollection();
                        foreach (Point point in strokePoints)
                        {
                            points.Add(new StylusPoint(point.X, point.Y));
                        }
                        Stroke newStroke = new Stroke(points);
                        inkCanvas.Strokes.Add(newStroke);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            });
        }

        private void InkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            var strokesData = new List<List<Point>>();
            foreach (var stroke in inkCanvas.Strokes)
            {
                var strokePoints = new List<Point>();
                foreach (var stylusPoint in stroke.StylusPoints)
                {
                    strokePoints.Add((Point)stylusPoint);
                }
                strokesData.Add(strokePoints);
            }

                                                    /*            string jsonStrokes = JsonConvert.SerializeObject(strokesData, Newtonsoft.Json.Formatting.Indented);
                                                                ws.Send(jsonStrokes)*/;


            var packet = new Packet(PacketType.DrawingData, 1, DateTime.Now, strokesData);
            var serializedPacket = packet.Serialize();
            if (sendCanvasActionsToServer && connectedToServer && ws.IsAlive)
            {
                ws.Send(serializedPacket);
            }
            else
            {
                CodeTextBlock.Text = "You are not connected to the Server!";
            }
            //WritePacketToFile(System.Text.Encoding.ASCII.GetBytes(serializedPacket), "packet.txt");

        }


        private void WritePacketToFile(byte[] packetContent, string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                {
                    fileStream.Write(packetContent, 0, packetContent.Length);
                }
                Console.WriteLine("Packet written to file successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing packet to file: " + ex.Message);
            }
        }

        private void CreateNewNotebook(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Title = "Create New Notebook";
            dialog.Filter = "NoteSync Files|.ns|All Files|.*";
            dialog.FileName = "New Notebook.ns";
            if (dialog.ShowDialog() == true)
            {
                selectedNotebookPath = dialog.FileName;
                try
                {
                    Directory.CreateDirectory(selectedNotebookPath);
                    Button notebookButton = new Button()
                    {
                        Content = Path.GetFileName(selectedNotebookPath),
                        Tag = selectedNotebookPath,
                        Margin = new Thickness(5),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(5),
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3E, 0x44, 0x4D)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0)
                    };
                    notebookButton.Click += NotebookButton_Click;
                    NotebooksPanel.Children.Add(notebookButton);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error creating notebook: " + ex.Message);
                }
            }
        }

        /*        private void NotebookButton_Click(object sender, RoutedEventArgs e)
                {
                    Button button = sender as Button;
                    if (button != null)
                    {
                        selectedNotebookPath = button.Tag.ToString();
                        CanvasStackPanel.Visibility = Visibility.Visible;
                    }
                }*/

        private void NotebookButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                selectedNotebookPath = button.Tag.ToString();
                CanvasStackPanel.Children.Clear(); 


                CanvasStackPanel.Visibility = Visibility.Visible;


                CanvasStackPanel.Children.Add(AddNewCanvasButton);
                CanvasStackPanel.Children.Add(PenButton);
                CanvasStackPanel.Children.Add(EraserButton);
                //CanvasStackPanel.Children.Add(AddTextBoxButton);
                //<Button x:Name="AddTextBoxButton" Content="Add Text Box" Click="AddTextBoxButton_Click"/>

                LoadCanvases(selectedNotebookPath);
            }
        }


        private void AddNewCanvas(string notebookPath)
        {

            var canvasDialog = new CanvasNameDialog();
            if (canvasDialog.ShowDialog() == true)
            {
                string canvasName = canvasDialog.CanvasName;
                string canvasFileName = canvasName + ".json";
                selectedCanvasFilePath = Path.Combine(notebookPath, canvasFileName);

                try
                {
                    File.Create(selectedCanvasFilePath).Close();
                    Console.WriteLine("Canvas file created at: " + selectedCanvasFilePath);

                    inkCanvas = new InkCanvas();
                    inkCanvas.StrokeCollected += InkCanvas_StrokeCollected;
/*                    inkCanvas.MouseDown += InkCanvas_MouseDown;
                    inkCanvas.MouseMove += InkCanvas_MouseMove;
                    inkCanvas.MouseUp +=InkCanvas_MouseUp;*/
                    //inkCanvas.Name(InkCanvasArea);
                    InkCanvasContainer.Children.Add(inkCanvas);

                    Button canvasButton = new Button
                    {
                        Content = canvasName,
                        Tag = selectedCanvasFilePath,
                        Margin = new Thickness(5),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(5),
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3E, 0x44, 0x4D)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0)
                    };
                    canvasButton.Click += CanvasButton_Click;
                    CanvasStackPanel.Children.Add(canvasButton);

                    Console.WriteLine("Canvas created at: " + selectedCanvasFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error creating canvas: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Canvas creation canceled.");
            }
        }



        private void AddNewCanvasClick(object sender, RoutedEventArgs e)
        {
            AddNewCanvas(selectedNotebookPath);
        }

        private void CanvasButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                if (inkCanvas.Parent != InkCanvasContainer)
                {
                    InkCanvasContainer.Children.Add(inkCanvas);
                }
                selectedCanvasFilePath = button.Tag.ToString();
                string canvasFilePath = button.Tag.ToString();
                DisplayCanvas(canvasFilePath);
            }
        }




        private void DisplayCanvas(string canvasFilePath)
        {
            try
            {
                if (inkCanvas != null)
                {
                    if (File.Exists(canvasFilePath))
                    {
                        string json = File.ReadAllText(canvasFilePath);

                        inkCanvas.Strokes.Clear();

                        List<List<Point>> strokesData = JsonConvert.DeserializeObject<List<List<Point>>>(json);
                        if (strokesData!=null)
                        {
                            foreach (List<Point> strokePoints in strokesData)
                            {
                                StylusPointCollection points = new StylusPointCollection();
                                foreach (Point point in strokePoints)
                                {
                                    points.Add(new StylusPoint(point.X, point.Y));
                                }
                                Stroke newStroke = new Stroke(points);
                                inkCanvas.Strokes.Add(newStroke);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Canvas file does not exist: " + canvasFilePath);
                    }
                }
                else
                {
                    MessageBox.Show("InkCanvas is not initialized.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading canvas: " + ex.Message);
            }
        }


        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("Hello world!");

            SaveStrokesToFile(inkCanvas, selectedCanvasFilePath);

        }

        private void SaveButton()
        {

            SaveStrokesToFile(inkCanvas, selectedCanvasFilePath);

        }

        private void PenButton_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.EditingMode = InkCanvasEditingMode.Ink;
            //RemoveTextBox();
        }

        private void EraserButton_Click(object sender, RoutedEventArgs e)
        {
            inkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;
            //RemoveTextBox();
        }


        private void SaveStrokesToFile(InkCanvas inkCanvas, string filePath)
        {
            bool containsImage = false;

            foreach (var child in inkCanvas.Children)
            {

                if (child is Image)
                {

                    containsImage = true;
                }
            }

            if (filePath != null && filePath != "")
            {
                var strokesData = new List<List<Point>>();
                foreach (var stroke in inkCanvas.Strokes)
                {
                    var strokePoints = new List<Point>();
                    foreach (var stylusPoint in stroke.StylusPoints)
                    {
                        strokePoints.Add(new Point(stylusPoint.X, stylusPoint.Y));
                    }
                    strokesData.Add(strokePoints);
                }

                string jsonStrokes = JsonConvert.SerializeObject(strokesData, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(filePath, jsonStrokes);
            }
        }

        private void OpenNotebookFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Select Notebook Folder";
            dialog.Filter = "Folder|.";
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;
            dialog.FileName = "Select Folder";

            if (dialog.ShowDialog() == true)
            {
                string notebookFolderPath = Path.GetDirectoryName(dialog.FileName);
                try
                {
                    Button notebookButton = new Button()
                    {
                        Content = Path.GetFileName(notebookFolderPath),
                        Tag = notebookFolderPath,
                        Margin = new Thickness(5),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(5),
                        Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3E, 0x44, 0x4D)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0)
                    };
                    notebookButton.Click += NotebookButton_Click;
                    NotebooksPanel.Children.Add(notebookButton);

                    LoadCanvases(notebookFolderPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error opening notebook folder: " + ex.Message);
                }
            }
        }



        private void LoadCanvases(string notebookFolderPath)
        {


            string[] canvasFiles = Directory.GetFiles(notebookFolderPath, "*.json");

            foreach (string canvasFile in canvasFiles)
            {
                selectedCanvasFilePath = Path.Combine(notebookFolderPath, canvasFile);
                Button canvasButton = new Button
                {
                    Content = Path.GetFileNameWithoutExtension(canvasFile),
                    Tag = selectedCanvasFilePath,
                    Margin = new Thickness(5),
                    Background = Brushes.LightGray
                };

                canvasButton.Click += CanvasButton_Click;
                CanvasStackPanel.Children.Add(canvasButton);
            }
        }

        private void JoinSessionButton_Click(object sender, RoutedEventArgs e)
        {

            TextBox textBox = FindName("code") as TextBox;
            if (textBox != null)
            {

                string textBoxText = textBox.Text;
                Debug.WriteLine("Text from TextBox: " + textBoxText);

                DateTime timeStamp = DateTime.Now;


                PacketType type = PacketType.JoinSession;
                int sequenceNumber = 1;

                Packet packet = new Packet(type, sequenceNumber, timeStamp, textBoxText);

                var serializedPacket = packet.Serialize();
                if (connectedToServer && ws.IsAlive)
                {
                    ws.Send(serializedPacket);
                }

                else
                {
                    CodeTextBlock.Text = "You are not connected to the Server!";
                }
               
            }
            else
            {
                Debug.WriteLine("TextBox not found!");
            }
        }


        private void CreateSessionButton_Click(object sender, RoutedEventArgs e)
        {

            DateTime timeStamp = DateTime.Now;


            PacketType type = PacketType.CreateSession;
            int sequenceNumber = 1;

            Packet packet = new Packet(type, sequenceNumber, timeStamp, null);

            var serializedPacket = packet.Serialize();
            if (connectedToServer && ws.IsAlive)
            {
                ws.Send(serializedPacket);
            }
            else
            {
                CodeTextBlock.Text = "You are not connected to the Server!";
            }
            //ws.Send(serializedPacket);
            //Console.WriteLine("console printing working");

        }

        private void DisconnectButtonClick(object sender, RoutedEventArgs e)
        {

            Packet closingPacket = new Packet(PacketType.CloseConnection, 1, DateTime.Now, "Closing connection");
            string serializedPacket = closingPacket.Serialize();

            if (connectedToServer && ws.IsAlive)
            {
                ws.Send(serializedPacket);
            }

            else if (ws != null && ws.IsAlive)
            {
                ws.Close();
                CodeTextBlock.Text = "Successfully Disconnected from the server!";
            }

            else
            {
                CodeTextBlock.Text = "You are already Disconnected from the Server!";
            }



            sendCanvasActionsToServer = false;
            connectedToServer = false;
        }
    }
}

public enum PacketType
{
    EstablishConnection,
    DrawingData,
    AddImage,
    AddText,
    CreateSession,
    SessionDetails,
    JoinSession,
    Login,
    CloseConnection,
}

public class Packet
{
    public Header PacketHeader { get; set; }
    public Body PacketBody { get; set; }

    public Packet(PacketType type, int sequenceNumber, DateTime timeStamp, object bodyData)
    {
        PacketHeader = new Header { Type = type, SequenceNumber = sequenceNumber, TimeStamp = timeStamp };
        PacketBody = new Body { Data = bodyData };
    }

    public string Serialize()
    {
        string json = JsonConvert.SerializeObject(this);
        return json;
        //return System.Text.Encoding.ASCII.GetBytes(json);
    }

    public static Packet Deserialize(byte[] data)
    {
        string json = System.Text.Encoding.ASCII.GetString(data);
        return JsonConvert.DeserializeObject<Packet>(json);
    }

    public class Header
    {
        public PacketType Type { get; set; }
        public int SequenceNumber { get; set; }
        public DateTime TimeStamp { get; set; }
    }

    public class Body
    {
        public object Data { get; set; }
    }
}




