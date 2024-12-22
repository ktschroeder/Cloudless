using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using static System.Formats.Asn1.AsnWriter;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Rectangle = System.Windows.Shapes.Rectangle;
using Brush = System.Windows.Media.Brush;
using System;
using Brushes = System.Windows.Media.Brushes;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Cloudless.MainWindow;
//using Rectangle = System.Windows.Shapes.Rectangle;

// ****** The slot approach is overly complicated and unwieldy. Put a random lifespan on each non-bg layer. Upon expiration, fade to 0 opacity, and fade in a new layer (can reuse layer or may be cleaner to start fresh: increment some index).
// As for the bg layer: options: 1) can have occasionally fade to opaque black. linger a while, then fade in new paint.
// 2) bg always solid black. First layer always 100% opacity. layer above becomes 100% at some point, then we remove the now-hidden layer. Keep this layer at 100 till likewise removed. Repeat forever.
// To ease future issues, from the start maintain a comprehensive state: queue of objects with rectangle layer, gradientstops, animations, etc.

namespace Cloudless
{
    public partial class MainWindow : Window
    {
        private const int StarSize = 2;   // Diameter of each star
        
        private Canvas StarsCanvas;

        private Random _random = new Random();
        private bool isZen;
        private bool isWelcome = true;
        private DispatcherTimer _resizeStarTimer;
        private int brushKey = 0;
        private Storyboard orchStoryboard = null;

        private List<GradientStopContext> gradientStopContexts = new List<GradientStopContext>();
        private int magicLayersCreated = 0;
        private const int CONCURRENT_ZEN_LAYERS = 4;

        private void InitializeZenMode()
        {
            _resizeStarTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400) // Adjust delay as needed
            };
            _resizeStarTimer.Tick += (s, e) =>
            {
                _resizeStarTimer.Stop();
                if (isZen) GenerateStars(); // Only regenerate stars once resizing stops
            };

            MyGrid.SizeChanged += (s, e) =>
            {
                ClearStars();
                _resizeStarTimer.Stop(); // Restart the timer on each size change
                _resizeStarTimer.Start();
            };
            _resizeStarTimer.Start();
        }

        private void Zen(bool includeInfoText)
        {
            // TODO wrap zen in a big try/catch and just disable it rather than crashing app

            RemoveZen(true);
            isZen = true;
            // via https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-animate-the-position-or-color-of-a-gradient-stop?view=netframeworkdesktop-4.8

            ImageDisplay.Visibility = Visibility.Collapsed;
            GradientMagic(); // Zen in context menu, also ability to unload image, possibly disable this
            GenerateStars();
            // Create the TextBlock for "No image is loaded" message
            if (includeInfoText)
            {
                if (!MyGrid.Children.Contains(NoImageMessage))
                {
                    MyGrid.Children.Add(NoImageMessage);
                    Canvas.SetZIndex(NoImageMessage, 99999);  // lazy
                }
                    
            }
            else if (MyGrid.Children.Contains(NoImageMessage))
            {
                MyGrid.Children.Remove(NoImageMessage);
            }
        }

        private void RemoveZen(bool leaveInfo = false)
        {
            if (!isZen)
                return;

            if (!leaveInfo)
                MyGrid.Children.Remove(NoImageMessage);

            // Stop the active Storyboard, if any
            foreach (var resourceKey in Resources.Keys)
            {
                if (Resources[resourceKey] is Storyboard storyboard)
                {
                    storyboard.Stop(this);
                }
            }

            // Unregister all gradient stops and layers
            for (int layer = 0; layer <= CONCURRENT_ZEN_LAYERS; layer++)  // TODO magic number, sync with creation of layers. revisit this for new cleanup following many changes
            {
                for (int i = 0; i <= 3; i++)
                {
                    string gradientStopName = $"GradientStop{i}Layer{layer}";
                    if (this.FindName(gradientStopName) is GradientStop)
                    {
                        this.UnregisterName(gradientStopName);
                    }
                }

                string gradientLayerName = $"GradientLayer{layer}";
                if (this.FindName(gradientLayerName) is Rectangle)
                {
                    this.UnregisterName(gradientLayerName);
                }
            }

            // Clear the background brush (for layer 0)
            this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 1 });

            // Remove all rectangles added to the Grid
            for (int i = MyGrid.Children.Count - 1; i >= 0; i--)
            {
                if (MyGrid.Children[i] is Rectangle)
                {
                    MyGrid.Children.RemoveAt(i);
                }
            }

            // Clear the storyboard children to release animations
            foreach (var resourceKey in Resources.Keys)
            {
                if (Resources[resourceKey] is Storyboard storyboard)
                {
                    storyboard.Stop(this);//
                    storyboard.Children.Clear();
                }
            }

            ClearStars();

            // Remove the canvas from the parent container
            if (MyGrid.Children.Contains(StarsCanvas))
            {
                MyGrid.Children.Remove(StarsCanvas);
            }

            // Set StarsCanvas to null to allow garbage collection
            StarsCanvas = null;

            if (currentlyDisplayedImagePath != null)
                ImageDisplay.Visibility = Visibility.Visible;

            isZen = false; // TODO check performance and that nothing is missed
        }

        private void Zen_Click(object sender, RoutedEventArgs e)
        {
            Zen(false);
        }


        private void ClearStars()
        {
            if (StarsCanvas == null) return;
            // clear animations to avoid memory leak
            foreach (UIElement child in StarsCanvas.Children)
            {
                if (child is FrameworkElement fe && fe.Tag is Storyboard storyboard)
                {
                    storyboard.Stop();
                    storyboard.Remove(); // Remove storyboard from the clock system
                    fe.BeginAnimation(UIElement.OpacityProperty, null); // Detach animations
                }
            }

            StarsCanvas.Children.Clear(); // Clear existing stars
        }

        private void GenerateStars()
        {
            if (StarsCanvas == null)
            {
                StarsCanvas = new Canvas
                {
                    Name = "StarsCanvas"
                };

            }
            else
            {
                ClearStars();
            }

            double width = MyGrid.ActualWidth;
            double height = MyGrid.ActualHeight;
            int starCount = 10 + (int)(width * height / 2500);  // 600 for 1920x1080 seemed good

            for (int i = 0; i < starCount; i++)
            {
                // Create a star (small circle)
                Ellipse star = new Ellipse
                {
                    Width = StarSize,
                    Height = StarSize,
                    Fill = Brushes.White,
                    Opacity = 0 // Start fully transparent
                };

                // Randomize initial position
                double startX = _random.NextDouble() * width;
                double startY = _random.NextDouble() * height;

                Canvas.SetLeft(star, startX);
                Canvas.SetTop(star, startY);

                // Add the star to the canvas
                StarsCanvas.Children.Add(star);

                // Create animations for fade-in, movement, and fade-out
                const double AnimationDurationSeconds = 8;
                var withDelay = AnimationDurationSeconds + _random.NextDouble() * 10.0;
                var startingDelay = _random.NextDouble() * 25.0;
                var periodDelay = 0.5 + _random.NextDouble() * 8.0;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(withDelay / 2.0)))
                {
                    BeginTime = TimeSpan.FromSeconds(periodDelay)
                };
                DoubleAnimation fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(withDelay / 2.0)))
                {
                    BeginTime = TimeSpan.FromSeconds(2*periodDelay + withDelay / 2.0) // Start fading out after fading in
                };

                // Apply animations
                Storyboard storyboard = new Storyboard
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    BeginTime = TimeSpan.FromSeconds(startingDelay)
                };

                Storyboard.SetTarget(fadeIn, star);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Ellipse.OpacityProperty));

                Storyboard.SetTarget(fadeOut, star);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Ellipse.OpacityProperty));

                storyboard.Children.Add(fadeIn);
                storyboard.Children.Add(fadeOut);

                star.Tag = storyboard;
                //Debug.WriteLine($"stars storyboard children: {storyboard.Children.Count}");
                // Start the animation
                storyboard.Begin(this, true);
            }

            if (!MyGrid.Children.Contains(StarsCanvas))
                MyGrid.Children.Add(StarsCanvas);

            Canvas.SetZIndex(StarsCanvas, 99998);  // lazy
        }

        

        private Duration DetermineLifespan(int layerIndex, int fadeInDurationSeconds)
        {
            const int lifespanQuadraticBase = 7; // 1 for debug. was 12.
            const int minLifespanSecondsBeyondFadeIn = 7;
            var originalLifespan = new Duration(TimeSpan.FromSeconds(fadeInDurationSeconds + minLifespanSecondsBeyondFadeIn + Math.Pow(_random.NextDouble() * lifespanQuadraticBase, 2)));
            var prevLayer = magicLayers.Where(ml => ml.layerIndex == layerIndex - 1).FirstOrDefault();
            if (prevLayer == null)
            {
                return originalLifespan;
            }

            var now = DateTime.Now;
            var prevLayerAge = now - prevLayer.birth;
            if (prevLayerAge > prevLayer.lifespan)  // in this case the layer is just waiting to be replaced by another
                return originalLifespan;
            var remaining = prevLayer.lifespan - prevLayerAge;
            return remaining + originalLifespan;
        }

        private double DetermineGradientAngle(int layerIndex)  // TODO merely assuming we are talking degrees and not radians. can also be improved: avoid other layers and avoid 180 degrees away.
        {
            var prevLayer = magicLayers.Where(ml => ml.layerIndex == layerIndex - 1).FirstOrDefault();
            if (prevLayer == null)
            {
                return _random.NextDouble() * 360;
            }

            var prevAngle = prevLayer.gradientAngle;
            const double berth = 20;
            var newAngle = prevAngle + berth + _random.NextDouble() * (360 - berth + prevAngle); // e.g. berth 20 prev 90 allows range 110 through 430 (430 is 70).
            return newAngle % 360;
        }

        private void GradientMagic()
        {
            // Create a NameScope for the page so that
            // Storyboards can be used.
            NameScope.SetNameScope(this, new NameScope());

            this.Background = new SolidColorBrush(new System.Windows.Media.Color() { ScA = 1 });
            magicLayersCreated = 0;

            for (int i = 0; i < CONCURRENT_ZEN_LAYERS; i++)
            {
                var ml = CreateMagicLayer();
                //ml.rectStoryboard.BeginTime = TimeSpan.FromSeconds(i * 2); // Stagger start times by 2 seconds.

                ml.rectStoryboard.Begin(this, true);
            }

            Debug.WriteLine("Started GradientMagic");
        }


        // TODO explore: timings? opacities? incorrect fills? things being removed from memory or not removed when needed?
        private MagicLayer CreateMagicLayer()
        {
            const double baseOpacity = 0.4;  // 0.69 was good, up from .49 and .34. Gets more interesting after initial mud phase.
            var mlLayerIndex = magicLayersCreated++;
            bool isFirstLayer = mlLayerIndex == 0;
            bool isFirstRound = mlLayerIndex < CONCURRENT_ZEN_LAYERS;
            int fadeInDurationSeconds = isFirstRound ? 0 : 8;
            var gradientStopStoryboard = new Storyboard();
            
            var mlBirth = DateTime.Now;
            // Checks lower layer's lifespan to ensure this one lasts beyond it.
            // This lifespan includes fade-in time. after this lifespan expires, this layer will transition to full opacity, retire the previous dead layer, and will be retired as soon as the next layer is ready to replace it.
            var mlLifeSpan = DetermineLifespan(mlLayerIndex, fadeInDurationSeconds); 
            var mlGradientAngle = DetermineGradientAngle(mlLayerIndex);  // Gets angle not too near to previous angle.
            var mlGscs = CreateGradientStopContexts(mlLayerIndex, gradientStopStoryboard);  // includes using the MainWindow's RegisterName to register each GradientStop.

            LinearGradientBrush gradientBrush = new LinearGradientBrush(new GradientStopCollection(mlGscs.Select(gsc => gsc.stop)), mlGradientAngle);

            Rectangle mlRect = new Rectangle()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Fill = gradientBrush,
                Opacity = isFirstLayer ? 1 : isFirstRound ? baseOpacity : 0
            };

            MyGrid.Children.Add(mlRect);
            var rectStoryboard = new Storyboard();
            var magicLayer = new MagicLayer()
            {
                gradientStopStoryboard = gradientStopStoryboard,
                layerIndex = mlLayerIndex,
                birth = mlBirth,
                lifespan = mlLifeSpan,
                gradientAngle = mlGradientAngle,
                gscs = mlGscs,
                rect = mlRect,
                rectStoryboard = rectStoryboard
            };

            // consider defining random seed for first-time use
            this.RegisterName("MyRect" + mlLayerIndex, mlRect);
            
            
            if (!isFirstRound)
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = baseOpacity,
                    Duration = TimeSpan.FromSeconds(fadeInDurationSeconds),
                    // BeginTime is 0: correct since we only got here after the previous layer completed
                    //EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }  // can consider omitting or using others
                };

                Storyboard.SetTargetName(fadeIn, "MyRect" + mlLayerIndex);
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(Rectangle.OpacityProperty));

                rectStoryboard.Children.Add(fadeIn);
            }

            // TODO about excluding below. if you just have a bunch of same-opacity layers not fluctuating in opacity then it looks like mud.
            ////3.remains at this opacity for its lifespan (bonus: also fluctuate layer opacity during this time, some.).
            ///
            double finalFluctuationOpacity = -1;
            if (!isFirstLayer)
            {
                var fluctuationAnimation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromSeconds(magicLayer.lifespan.TimeSpan.TotalSeconds - fadeInDurationSeconds),
                    BeginTime = TimeSpan.FromSeconds(fadeInDurationSeconds),
                };
                const double MIN_FLUX_SECONDS = 2;
                const double MAX_FLUX_SECONDS = 20;
                const double MIN_FLUX_OPACITY = 0.02;
                const double MAX_FLUX_OPACITY = 0.5;  // itentionally disobeyed in while-loop for final fluctuation, to align well with other animations
                double timeToFluctuateSeconds = magicLayer.lifespan.TimeSpan.TotalSeconds - fadeInDurationSeconds;
                int fluxCount = 0;

                while (finalFluctuationOpacity == -1) // checking this instead of time in case of epsilon issue
                {
                    var thisFluctuationOpacity = MIN_FLUX_OPACITY + _random.NextDouble() * (MAX_FLUX_OPACITY - MIN_FLUX_OPACITY);
                    var thisFluctuationSeconds = MIN_FLUX_SECONDS + _random.NextDouble() * (MAX_FLUX_SECONDS - MIN_FLUX_SECONDS);
                    // if this will be so long that the next flux would be shorter than min length... (we are okay going over defined max in this edge case)
                    if (thisFluctuationSeconds + MIN_FLUX_SECONDS >= timeToFluctuateSeconds)
                    {
                        thisFluctuationSeconds = timeToFluctuateSeconds;  // use all remaining time
                        finalFluctuationOpacity = thisFluctuationOpacity;
                    }
                    timeToFluctuateSeconds -= thisFluctuationSeconds;

                    fluctuationAnimation.KeyFrames.Add(new SplineDoubleKeyFrame(
                        thisFluctuationOpacity,
                        KeyTime.FromTimeSpan(TimeSpan.FromSeconds(thisFluctuationSeconds)),
                        new KeySpline(0.25, 0.1, 0.25, 1) // Smooth easing. Can revisit.
                    ));
                    fluxCount++;
                }

                Debug.Print($"Made a batch of {fluxCount} fluctuations totaling {magicLayer.lifespan.TimeSpan.TotalSeconds - fadeInDurationSeconds} seconds");
                Storyboard.SetTarget(fluctuationAnimation, magicLayer.rect);
                Storyboard.SetTargetProperty(fluctuationAnimation, new PropertyPath(Rectangle.OpacityProperty));
                rectStoryboard.Children.Add(fluctuationAnimation);
            }
            
            //4.After lifespan, transition opacity to 1.0.
            var fadeToFull = new DoubleAnimation
            {
                From = isFirstLayer ? 1 : finalFluctuationOpacity,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(7),  // making this a constant value is convenient so there is not a possibility of the next layer trying to delete this one before reaching full opacity. Otherwise include in lifespan calculations to not risk occasional flashes.
                BeginTime = magicLayer.lifespan.TimeSpan,
                //EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }  // can consider omitting or using others
            };
            Storyboard.SetTarget(fadeToFull, magicLayer.rect);
            Storyboard.SetTargetProperty(fadeToFull, new PropertyPath(Rectangle.OpacityProperty));
            rectStoryboard.Children.Add(fadeToFull);

            //5.Once at 1.0, delete and free the layer that was previously at 1.0(it's the layer with one-lower index). Remain at 1.0 until another layer does the same.
            rectStoryboard.Completed += (s, e) =>
            {
                // Dequeue the old layer and free its resources.
                if (!isFirstLayer && magicLayers.TryDequeue(out var expiredLayer))
                {
                    if (expiredLayer.layerIndex != mlLayerIndex - 1)
                        throw new Exception("bad");
                    expiredLayer.Free(this, MyGrid);
                }

                var ml = CreateMagicLayer(); // This is infinite recursion (though limited in contant space); may want to check that this is cleared as expected when exiting/resetting zen.
                ml.rectStoryboard.Begin(this, true);
            };

            magicLayers.Enqueue(magicLayer);
            gradientStopStoryboard.Begin(this, true);
            Debug.WriteLine("created magic layer with index " + magicLayer.layerIndex + " lifespan " + magicLayer.lifespan);
            return magicLayer;
        }

        internal class GradientStopContext
        {
            internal GradientStop stop;
            //internal int? stopIndex;
            internal string? name;
            internal AnimationTimeline? offsetAnimation;
            internal AnimationTimeline? colorAnimation;
        }

        private Queue<MagicLayer> magicLayers = new Queue<MagicLayer>();

        internal class MagicLayer
        {
            internal Rectangle rect;  // has opacity and fill
            internal DateTime birth;
            internal Duration lifespan;
            internal Storyboard gradientStopStoryboard;
            internal Storyboard rectStoryboard;
            internal int layerIndex;
            internal List<GradientStopContext> gscs = new List<GradientStopContext>();
            internal double gradientAngle; 

            internal void Free(MainWindow mainWindow, Grid parentOfRect) // pass in MainWindow ("this") and MyGrid
            { // TODO should consider both types of storyboard and other recent changes. maybe we can set alerts for memory leaks too.
                parentOfRect.Children.Remove(rect);

                foreach (var gsc in gscs)
                {
                    mainWindow.UnregisterName(gsc.name);
                    if (gsc.offsetAnimation != null)
                        gradientStopStoryboard.Children.Remove(gsc.offsetAnimation); // TODO anything more needed to release this resource?
                    if (gsc.colorAnimation != null)
                        gradientStopStoryboard.Children.Remove(gsc.colorAnimation);
                    gradientStopStoryboard.Stop();
                    gradientStopStoryboard.Remove();
                }

                mainWindow.UnregisterName("MyRect" + layerIndex);
                rectStoryboard.Children.Clear();
                rectStoryboard.Stop();
                rectStoryboard.Remove();
            }
        }

        private List<GradientStopContext> CreateGradientStopContexts(int layer, Storyboard storyboard)
        {
            GradientStopContext gsc0 = new GradientStopContext();
            GradientStopContext gsc1 = new GradientStopContext();
            GradientStopContext gsc2 = new GradientStopContext();
            GradientStopContext gsc3 = new GradientStopContext();

            // Create gradient stops for the brush.
            var tweak = (float)(_random.NextDouble() * 0.3 - 0.1);
            gsc0.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0.5F - tweak, 0F, 0.5F + tweak), 0.0);
            gsc1.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0F, 0F, 0.5F + tweak), 0.3);
            gsc2.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0.6F + tweak, 0F, 0.8F - tweak), 0.6);
            gsc3.stop = new GradientStop(System.Windows.Media.Color.FromScRgb(1F, 0F, 0.4F + tweak, 0.63F + tweak), 1.0);

            gsc0.name = "GradientStop0Layer" + layer;
            gsc1.name = "GradientStop1Layer" + layer;
            gsc2.name = "GradientStop2Layer" + layer;
            gsc3.name = "GradientStop3Layer" + layer;

            // Register a name for each gradient stop with the
            // page so that they can be animated by a storyboard.
            this.RegisterName(gsc0.name, gsc0.stop);
            this.RegisterName(gsc1.name, gsc1.stop);
            this.RegisterName(gsc2.name, gsc2.stop);
            this.RegisterName(gsc3.name, gsc3.stop);

            // We've intentionally skipped index 0 to not animate that gradient stop.
            var oa1 = CreateOffsetAnimation(gsc1.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.03, 0.30);
            var oa2 = CreateOffsetAnimation(gsc2.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.73, 0.37);
            var oa3 = CreateOffsetAnimation(gsc3.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.97, 0.8);
            storyboard.Children.Add(oa1);
            storyboard.Children.Add(oa2);
            storyboard.Children.Add(oa3);
            gsc1.offsetAnimation = oa1;
            gsc2.offsetAnimation = oa2;
            gsc3.offsetAnimation = oa3;

            Action<GradientStopContext> createOpacityOrColorAnim = gsc => 
            {
                if (layer > 0 && _random.NextDouble() > 0.5)
                {
                    // opacity anim
                    if (gsc.name == null) throw new Exception();
                    var opacityOA = CreateOpacityOrColorAnimation(gsc.name, TimeSpan.FromSeconds(10 + _random.NextDouble() * 20), System.Windows.Media.Color.FromScRgb(_random.NextDouble()>0.5? -1.0F : 1.0F, 0F, 0F, 0F));
                    gsc.colorAnimation = opacityOA;
                    storyboard.Children.Add(opacityOA);
                }
                else
                {
                    //color anim
                    var tweak1 = (float)(0.1 + _random.NextDouble() * 0.55);
                    var tweak2 = (float)(0.1 + _random.NextDouble() * 0.55);
                    var tweak3 = (float)(0.1 + _random.NextDouble() * 0.55);
                    if (gsc.name == null) throw new Exception();
                    var colorOA = CreateOpacityOrColorAnimation(gsc.name, TimeSpan.FromSeconds(10 + _random.NextDouble() * 20), System.Windows.Media.Color.FromScRgb(0F, tweak1, tweak2, tweak3));
                    gsc.colorAnimation = colorOA;
                    storyboard.Children.Add(colorOA);
                }
            };
            createOpacityOrColorAnim(gsc0);
            createOpacityOrColorAnim(gsc1);
            createOpacityOrColorAnim(gsc2);
            createOpacityOrColorAnim(gsc3);

            return new List<GradientStopContext>() { gsc0, gsc1, gsc2, gsc3 };  // can be simplified; do this earlier in method and use foreachs
        }

        private DoubleAnimation CreateOffsetAnimation(string target, Duration duration, double from, double to)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTargetName(animation, target);
            Storyboard.SetTargetProperty(animation, new PropertyPath(GradientStop.OffsetProperty));
            return animation;
        }

        private ColorAnimation CreateOpacityOrColorAnimation(string targetName, Duration duration, System.Windows.Media.Color colorBy)
        {
            ColorAnimation animation = new ColorAnimation
            {
                By = colorBy,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTargetName(animation, targetName);
            Storyboard.SetTargetProperty(animation, new PropertyPath(GradientStop.ColorProperty));
            return animation;
        }

        
    }
}
