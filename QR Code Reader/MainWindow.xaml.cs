using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AForge.Video.DirectShow;
using AForge.Video;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Media;
using ZXing.QrCode;
using ZXing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.IO;

namespace QR_Code_Reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int camSelected = 0;

        FilterInfoCollection camSources;
        VideoCaptureDevice camVideo;

        // A stopWatch to test the processing speed.
        Stopwatch stopwatch = new Stopwatch();

        // Bitmap buffers
        Bitmap streamBitmap;
        Bitmap snapShotBitmap;
        Bitmap safeTempstreamBitmap;

        // Sound to be played when successful detection take a place.
        SoundPlayer player = new SoundPlayer(@"K:\QRCodeReader\Resources\digi_chime_up.wav");

        // Thread for decoding in parallel with the webcam video streaming.
        Thread decodingThread;

        // The QR Decoder variable from ZXing
        QRCodeReader decoder;

        public MainWindow()
        {
            InitializeComponent();
        }

        void videoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                streamBitmap = (Bitmap)eventArgs.Frame.Clone();
                safeTempstreamBitmap = (Bitmap)streamBitmap.Clone();
                //pictureBox1.Source = ImageSourceForBitmap(safeTempstreamBitmap);
                MemoryStream ms = new MemoryStream();
                streamBitmap.Save(ms, ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.StreamSource = ms;
                bi.EndInit();

                bi.Freeze();
                Dispatcher.BeginInvoke(new ThreadStart(delegate
                {
                    pictureBox1.Source = bi;
                }));
            }
            catch (Exception exp)
            {
                Console.Write(exp.Message);
            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        public void decodeLoop()
        {
            while (true)
            {
                // 1 second pause for the thread. This could be changed manually to a prefereable decoding interval.
                Thread.Sleep(1000);
                if (streamBitmap != null)
                    snapShotBitmap = (Bitmap)safeTempstreamBitmap.Clone();
                else
                    return;

                // Reset watch before decoding the streamed image.
                stopwatch.Reset();
                stopwatch.Start();

                // Decode the snapshot.
                LuminanceSource source;
                source = new BitmapLuminanceSource(snapShotBitmap);
                BinaryBitmap bitmap = new BinaryBitmap(new ZXing.Common.HybridBinarizer(source));
                Result result = new MultiFormatReader().decode(bitmap);
                //string decodeStr = decoder.decode(snapShotBitmap);
                

                stopwatch.Stop();
                //string decode = Detect(b);

                // If decodeStr is null then there was no QR detected, otherwise show the result of detection and play the sound.
                if (result == null)
                {
                    //System.Windows.MessageBox.Show("There is no QR Code!");
                }
                else
                {
                    player.Play();
                    if (result.ToString().Substring(0, 4) == "http")
                    {
                        System.Diagnostics.Process.Start(result.ToString());
                    }
                    else
                    {
                            Dispatcher.Invoke(() =>
                            {
                                if (result.ToString() != consoleBox.Text)
                                {
                                    consoleBox.AppendText(result.ToString());
                                }     
                            });
                            Thread.Sleep(20000);
                        Dispatcher.Invoke(() =>
                        {
                            consoleBox.Text = "";
                        });
                        //System.Windows.MessageBox.Show(result.ToString());
                        //System.Windows.MessageBox.Show(stopwatch.Elapsed.TotalMilliseconds.ToString());
                    }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize sound variable
            //player.Stream = Properties.Resources.connect;

            decoder = new QRCodeReader();

            // Start a decoding process
            decodingThread = new Thread(new ThreadStart(decodeLoop));
            decodingThread.Start();

            try
            {
                // enumerate video devices
                camSources = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (camSources.Count < 1)
                {
                    System.Windows.MessageBox.Show("No camera detected.");
                    System.Environment.Exit(0);
                }
                else
                {
                    camStream(camSelected);
                }

            }
            catch (VideoException exp)
            {
                Console.Write(exp.Message);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            decodingThread.Abort();
            camVideo.Stop();
            stopwatch.Stop();           
        }

        private void exitButton_Click(object sender, RoutedEventArgs e)
        {
            System.Environment.Exit(0);
        }

        public void toggleCam_Click(object sender, RoutedEventArgs e)
        {
            if (camSources.Count > 1)
            {
                decodingThread.Abort();
                camVideo.Stop();
                try
                {
                    decodingThread = new Thread(new ThreadStart(decodeLoop));
                    if (camSelected == 0)
                    {
                        camStream(1);
                    }
                    else
                    {
                        camStream(0);
                    }
                    decodingThread.Start();
                }
                catch (Exception exp)
                {
                    Console.Write(exp);
                }
            }
        }

        public void camStream(int camNum)
        {
            try
            {
                camVideo = new VideoCaptureDevice(camSources[camNum].MonikerString);
                camVideo.NewFrame += new NewFrameEventHandler(videoSource_NewFrame);
                camVideo.Start();
                camSelected = camNum;
            }
            catch (VideoException exp)
            {
                Console.Write(exp.Message);
            }
        }
    }
}
