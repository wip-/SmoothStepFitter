using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.IO;
using System.Windows.Interop;

namespace SmoothStepFitter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Bitmap imageBitmap;
        public byte[] imageCurve;
        public BitmapInfo imageBitmapInfo;
        public BitmapInfo curveBitmapInfo;
        

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            double vert = ((ScrollViewer)sender).VerticalOffset;
            double hori = ((ScrollViewer)sender).HorizontalOffset;

            ScrollViewer[] scrollViewers = new ScrollViewer[]
            {
                ScrollViewerImage, 
                ScrollViewerImageCurve, 
                ScrollViewerMyCurve,
                ScrollViewerDiff,
            };

            foreach (ScrollViewer scrollViewer in scrollViewers)
            {
                if (scrollViewer == null)
                    continue;
                scrollViewer.ScrollToVerticalOffset(vert);
                scrollViewer.ScrollToHorizontalOffset(hori);
                scrollViewer.UpdateLayout();
            }
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            int x = (int)(e.GetPosition(Image).X);
            int y = (int)(e.GetPosition(Image).Y);
            var color = imageBitmapInfo.GetPixelColor(x, y);
            LabelInfo.Content = String.Format("X={0:D4}, Y={1:D4}, A={2:D3}, R={3:D3}, G={4:D3}, B={5:D3}",
                x, y, color.A, color.R, color.G, color.B);
        }

        private void SliderZoomOut_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderZoomOut.Value != 1)
                Zoom(SliderZoomOut.Value);
            if (SliderZoomIn != null)
                SliderZoomIn.Value = 1;
        }

        private void SliderZoomIn_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SliderZoomIn.Value != 1)
                Zoom(SliderZoomIn.Value);
            if (SliderZoomOut != null)
                SliderZoomOut.Value = 1;
        }

        private void ButtonResetZoom_Click(object sender, RoutedEventArgs e)
        {
            Zoom(1);
            SliderZoomIn.Value = 1;
            SliderZoomOut.Value = 1;
        }

        private double GetCurrentZoom()
        {
            if (SliderZoomIn.Value != 1)
                return SliderZoomIn.Value;
            if (SliderZoomOut.Value != 1)
                return SliderZoomIn.Value;
            return 1;
        }

        private void Zoom(double val)
        {
            try
            {
                ScaleTransform myScaleTransform = new ScaleTransform();
                myScaleTransform.ScaleY = val;
                myScaleTransform.ScaleX = val;
                if (LabelZoom != null)
                    LabelZoom.Content = val;
                TransformGroup myTransformGroup = new TransformGroup();
                myTransformGroup.Children.Add(myScaleTransform);

                System.Windows.Controls.Image[] images = new System.Windows.Controls.Image[] 
                { 
                    Image,
                    ImageCurve,
                    MyCurve,                    
                };

                foreach (System.Windows.Controls.Image image in images)
                {
                    if (image == null || image.Source == null)
                        continue;
                    //image.RenderTransform = myTransformGroup;
                    image.LayoutTransform = myTransformGroup;
                }
            }
            catch (System.Exception ex)
            {
                Helpers.MyCatch(ex);
            }
        }

        private void Image_Drop(object sender, DragEventArgs e)
        {
            String msg = LoadImage(Image, ref imageBitmap, e);

            // TODO use async / await

            if (msg != null)
            {
                LabelInfo.Content = msg;
            }
            else
            {
                UpdateImageCurve();
                UpdateMyCurve();
            }
        }



        private double SmoothStep(double x)
        {
            return 3 * x * x - 2 * x * x * x;
        }

        private double ATanStep(double x)
        {
            return 0.5f + Math.Atan(50*(x-0.5))/Math.PI;
        }

        private double ScaledSmoothStep(double x, double w)
        {
            double y;

            if (x <= 0.5 - 0.5*w)
                y = 0.0;
            else if (x >= 0.5 + 0.5*w)
                y = 1.0;
            else
            {
                double scaled_x = (2 * x + w - 1) / (2 * w);
                y = SmoothStep(scaled_x);
            }

            return y;
        }

        private void UpdateMyCurve()
        {
            byte[] myCurve = CreateMyCurve(curveBitmapInfo);
            BitmapInfo myCurveBitmapInfo = myCurve.ToBitmapInfo();
            MyCurve.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    myCurveBitmapInfo.ToBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            BitmapInfo diffBitmapInfo = CreateDiffCurve(imageCurve, myCurve).ToBitmapInfo();

            Diff.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    diffBitmapInfo.ToBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        private byte[] CreateDiffCurve(byte[] left, byte[] right)
        {
 	        byte[] dest = new byte[left.Length];

            for (int x = 0; x < left.Length; x++)
            {
                double diff = left[x]-right[x];
                diff = (255*(diff+255)/510);
                dest[x] = Convert.ToByte(diff.Clamp0_255());
            }
            return dest;
        }

        private byte[] ExtractImageCurve(BitmapInfo src)
        {
            byte[] dest = new byte[src.Width];

            for (int x = 0; x < src.Width; x++)
            {
                System.Drawing.Color srcColor = src.GetPixelColor(x, 256);
                dest[x] = srcColor.R;
            }

            return dest;
        }

        private byte[] CreateMyCurve(BitmapInfo bitmapInfo)
        {
            byte[] dest = new byte[bitmapInfo.Width];

            var white = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            var black = System.Drawing.Color.FromArgb(255, 0, 0, 0);

            for (int x = 0; x < bitmapInfo.Width; x++)
            {
                // normalized values
                double x_n = (float)x / (bitmapInfo.Width - 1);
                double y_n;

                //y_n = SmoothStep(x_n);
                //y_n = ATanStep(x_n);
                y_n = ScaledSmoothStep(x_n, SliderSmoothStepWidth.Value);

                dest[x] = y_n.ScaleToByte();
            }

            return dest;
        }

        private void MyCatch(System.Exception ex)
        {
            var st = new StackTrace(ex, true);      // stack trace for the exception with source file information
            var frame = st.GetFrame(0);             // top stack frame
            String sourceMsg = String.Format("{0}({1})", frame.GetFileName(), frame.GetFileLineNumber());
            Console.WriteLine(sourceMsg);
            MessageBox.Show(ex.Message + Environment.NewLine + sourceMsg);
            Debugger.Break();
        }

        private String LoadImage(
            System.Windows.Controls.Image destinationImage,
            ref Bitmap destinationBitmap, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return "Not a file!";

                String[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 1)
                    return "Too many files!";

                String imageSourceFileName = files[0];

                if (!File.Exists(imageSourceFileName))
                    return "Not a file!";

                return LoadInputImage(destinationImage, ref destinationBitmap, imageSourceFileName);
            }
            catch (System.Exception ex)
            {
                MyCatch(ex);
                return "Exception";
            }
        }

        private static Bitmap LoadBitmap(String filename, out String errorMessage)
        {
            FileStream fs = null;
            try
            {
                fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                if (fs != null)
                    fs.Close();
                errorMessage = "File already in use!";
                return null;
            }

            Bitmap bitmap;
            //try
            {
                bitmap = new Bitmap(fs);
                errorMessage = null;
            }
            //catch (System.Exception /*ex*/)
            //{
            //    bitmap.Dispose();
            //    errorMessage = "Not an image!";
            //}
            return bitmap;
        }

        private void SliderSmoothStepWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LabelSliderSmoothStepWidth.Content = SliderSmoothStepWidth.Value;

            UpdateMyCurve();
        }

        private void SliderInputImage_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int imageIndex = Convert.ToInt32(SliderInputImage.Value);
            String[] images = Directory.GetFiles("F:\\gradients");
            String msg = LoadInputImage(Image, ref imageBitmap, images.ElementAt(imageIndex));
            if(msg!=null)
                LabelInfo.Content = msg;
            else
                UpdateImageCurve();
        }

        // return error message if any
        private String LoadInputImage(System.Windows.Controls.Image destinationImage, ref Bitmap destinationBitmap, String imagePath)
        {
            LabelFileName.Content = System.IO.Path.GetFileNameWithoutExtension(imagePath);

            String errorMessage;
            destinationBitmap = LoadBitmap(imagePath, out errorMessage);
            if (errorMessage != null)
                return errorMessage;

            destinationImage.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    destinationBitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            return null;
        }

        private void UpdateImageCurve()
        {
            imageBitmapInfo = new BitmapInfo(imageBitmap);
            imageCurve = ExtractImageCurve(imageBitmapInfo);
            curveBitmapInfo = imageCurve.ToBitmapInfo();
            ImageCurve.Source =
                Imaging.CreateBitmapSourceFromHBitmap(
                    curveBitmapInfo.ToBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        }


    }
}
