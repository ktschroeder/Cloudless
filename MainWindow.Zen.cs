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

        private List<GradientStopContext> gradientStopContexts = new List<GradientStopContext>();

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
            for (int layer = 0; layer <= 5; layer++)  // TODO magic number, sync with creation of layers
            {
                for (int i = 0; i <= 3; i++)
                {
                    string gradientStopName = $"GradientStop{i}Layer{layer}";
                    if (this.FindName(gradientStopName) is GradientStop)
                    {
                        this.UnregisterName(gradientStopName);
                    }
                    if (this.FindName(gradientStopName + "Odd") is GradientStop)
                    {
                        this.UnregisterName(gradientStopName + "Odd");
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
        }

        private void AnimateCrossfade(Rectangle rect, Brush fromBrush, Brush toBrush, int layer, bool isOddIteration, List<AnimationTimeline> currentInternalAnimations, Storyboard storyboard)
        {
            // Overlay the next brush on top of the current one using OpacityMask.
            Rectangle overlayRect = new Rectangle
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Fill = toBrush,
                Opacity = 0
            };

            MyGrid.Children.Add(overlayRect); // Add the overlay rectangle.

            // Fade in the new brush and fade out the old one.
            //DoubleAnimation fadeIn = new DoubleAnimation(0, 0.34, TimeSpan.FromSeconds(2));  // Fade in new.
            //DoubleAnimation fadeOut = new DoubleAnimation(0.34, 0, TimeSpan.FromSeconds(2)); // Fade out current.

            ColorAnimation fadeIn = new ColorAnimation
            {
                By = System.Windows.Media.Color.FromScRgb(0.34F, 0F, 0F, 0F),
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = new RepeatBehavior(count:0)
                //EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                //BeginTime = TimeSpan.FromSeconds(2 * layer - 1),  // expects layer to be at least 1 // TODO maybe only have this be nonzero on first round. getting messy...
            };
            string toTarget = "ToRectangleLayer" + layer + (isOddIteration ? "Odd" : "");
            this.RegisterName(toTarget, toBrush);
            Storyboard.SetTargetName(fadeIn, toTarget);  // $"GradientStop{i}Layer{layer}" + (isOddIteration ? "Odd" : "")
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(GradientStop.ColorProperty));
            storyboard.Children.Add(fadeIn);  // TODO also clean these up

            ColorAnimation fadeOut = new ColorAnimation
            {
                By = System.Windows.Media.Color.FromScRgb(-0.34F, 0F, 0F, 0F),
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = new RepeatBehavior(count:0)
                //EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                //BeginTime = TimeSpan.FromSeconds(2 * layer - 1),  // expects layer to be at least 1 // TODO maybe only have this be nonzero on first round. getting messy...
            };
            string fromTarget = "FromRectangleLayer" + layer + (isOddIteration ? "Odd" : "");
            this.RegisterName(fromTarget, fromBrush);
            Storyboard.SetTargetName(fadeOut, fromTarget);  // $"GradientStop{i}Layer{layer}" + (isOddIteration ? "Odd" : "")
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(GradientStop.ColorProperty));
            storyboard.Children.Add(fadeOut);

            fadeIn.Changed += (s, e) =>
            {
                Debug.Print("asdasd");
            };

            // Start animations.
            fadeIn.Completed += (s, e) =>
            {
                Debug.WriteLine("Finished fadeIn in AnimateCrossfade.");  // or WriteLine


                // After transition, set the main rectangle's fill to the new brush. //TODO the fact this isn't being hit may explain the snappish behavior.
                rect.Fill = toBrush;
                rect.Opacity = 0.34;

                // Remove the overlay rectangle.
                MyGrid.Children.Remove(overlayRect);

                // TODO probably unregister stuff here. may need to invert logic for isOdd.
                this.UnregisterName("GradientStop0Layer" + layer + (!isOddIteration ? "Odd" : ""));
                this.UnregisterName("GradientStop1Layer" + layer + (!isOddIteration ? "Odd" : ""));
                this.UnregisterName("GradientStop2Layer" + layer + (!isOddIteration ? "Odd" : ""));
                this.UnregisterName("GradientStop3Layer" + layer + (!isOddIteration ? "Odd" : ""));
                foreach (AnimationTimeline timeline in currentInternalAnimations)
                {
                    storyboard.Children.Remove(timeline);  // opacity/color and transform animations. TODO anything more needed to release this resource?
                }

                this.UnregisterName(toTarget);
                this.UnregisterName(fromTarget);
                storyboard.Children.Remove(fadeIn);
                storyboard.Children.Remove(fadeOut);
            };

            // TODO maybe issue that these are not in storyboard? putting them in storyboard would also allow cleaner BeginTime.
            //overlayRect.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            //rect.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        //TODO it seems like every other time I run the app, then every other "layer" that pops into view is just a solid pinkish color. Weird. race condition maybe.
        // TODO explore: timings? opacities? incorrect fills? tings being removed from memory or not removed when needed?
        private void CreateMagicLayer(int layer, int gradientAngle, Storyboard storyboard)
        {
            // track whether this is the first/third/fifth/etc. iteration, so we know whether to inform the creation method to use "alt" in resource names.
            // This approach makes clean-up more efficient. (First will be even: 0-based.)
            bool isOddIteration = false;

            // Create initial brushes
            (LinearGradientBrush currentBrush, List<AnimationTimeline> currentInternalAnimations) = CreateAnimatedGradientBrush(layer, gradientAngle, storyboard, isOddIteration);
            LinearGradientBrush nextBrush = null;// CreateAnimatedGradientBrush(layer, gradientAngle, null); // No animations initially
            List<AnimationTimeline> nextInternalAnimations = null;

            Rectangle rect = new Rectangle();
            if (layer == 0)
            {
                this.Background = currentBrush;
            }
            else
            {
                rect.HorizontalAlignment = HorizontalAlignment.Stretch; // Expand to container width
                rect.VerticalAlignment = VerticalAlignment.Stretch;     // Expand to container height
                rect.Fill = currentBrush;
                rect.Opacity = 0.34;

                // TODO we can replace this with the new opacity magic?
                //if (layer > 2)
                //{
                //    rect.Opacity = 0;
                //    this.RegisterName("GradientLayer" + layer, rect);
                //    var layerOpacityOA = CreateOpacityOrColorAnimation($"GradientLayer{layer}", TimeSpan.FromSeconds(10 + _random.NextDouble() * 30), System.Windows.Media.Color.FromScRgb(0.4F, 0F, 0F, 0F), TimeSpan.FromSeconds((layer - 2) * 5));
                //}
                MyGrid.Children.Add(rect);
            }

            if (layer == 0) return; // return to bg case TODO


            // DispatcherTimer for periodic brush transitions
            DispatcherTimer brushTransitionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(15), // Trigger every 15 seconds (was originally 30)
            };

            brushTransitionTimer.Tick += (sender, e) =>
            {
                Debug.WriteLine("brushTransitionTimer Tick");
                isOddIteration = !isOddIteration;

                // Define a new "nextBrush" with random gradient properties
                (nextBrush, nextInternalAnimations) = CreateAnimatedGradientBrush(layer, _random.Next(0, 360), storyboard, isOddIteration); // TODO null storyboard? can we pass in other storyboard? or new one? with cleaning at each tick if needed?

                // Create animations for opacity transitions
                //DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(10)); // Fade out current brush
                //DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(10));  // Fade in next brush

                //// Create a storyboard for the transition  //TODO clean this too in clean-up
                //Storyboard transitionStoryboard = new Storyboard();
                //Storyboard.SetTarget(fadeOut, rect);
                //Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
                //Storyboard.SetTarget(fadeIn, rect);
                //Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));

                //transitionStoryboard.Children.Add(fadeOut);
                //transitionStoryboard.Children.Add(fadeIn);

                //// Replace the current brush after the transition completes
                //transitionStoryboard.Completed += (s, args) =>
                //{
                //    rect.Fill = nextBrush;
                //    currentBrush = nextBrush; // Update reference
                //};

                //// Start the transition
                //transitionStoryboard.Begin();


                // Animate the rectangle's brush transition.
                AnimateCrossfade(rect, currentBrush, nextBrush, layer, isOddIteration, currentInternalAnimations, storyboard);

                // After the crossfade, replace the current brush reference.
                currentBrush = nextBrush;
                currentInternalAnimations = nextInternalAnimations;
            };

            // Start the timer for brush transitions
            //System.Threading.Thread.Sleep(1000 * layer);  // may cause issues, it's not as if each iteration of this loop is on its own thread
            //System.Threading.Thread.Sleep(1000);  // may cause issues, it's not as if each iteration of this loop is on its own thread
            brushTransitionTimer.Start();
        }

        internal class GradientStopContext
        {
            internal GradientStop stop;
            internal int? layer;
            internal bool? isOddIteration;
            internal int? stopIndex;
            internal string? name;
            internal AnimationTimeline? offsetAnimation;
            internal AnimationTimeline? colorAnimation;
        }

        //private void AddGSC(GradientStop stop, int layer, bool isOddIteration, int stopIndex, string name, AnimationTimeline? animation)
        private void AddGSC(GradientStopContext gsc)
        {
            if (gsc.stop == null || gsc.layer == null || gsc.isOddIteration == null || gsc.stopIndex == null || gsc.name == null)
                throw new InvalidOperationException("incomplete gsc");
            if (GetGSC(gsc.name) != null) 
                throw new InvalidOperationException("GSC already exists: " + gsc.name);

            gradientStopContexts.Add(gsc);
        }

        private GradientStopContext? GetGSC(string name)
        {
            return gradientStopContexts.Where(gsc => gsc.name.Equals(name)).FirstOrDefault();     
        }

        private (LinearGradientBrush, List<AnimationTimeline>) CreateAnimatedGradientBrush(int layer, int gradientAngle, Storyboard storyboard, bool isOddIteration)
        {
            var animations = new List<AnimationTimeline>();

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

            LinearGradientBrush gradientBrush = new LinearGradientBrush(
                new GradientStopCollection() { gsc0.stop, gsc1.stop, gsc2.stop, gsc3.stop },
                gradientAngle
                );

            gsc0.name = "GradientStop0Layer" + layer + (isOddIteration ? "Odd" : "");
            gsc1.name = "GradientStop1Layer" + layer + (isOddIteration ? "Odd" : "");
            gsc2.name = "GradientStop2Layer" + layer + (isOddIteration ? "Odd" : "");
            gsc3.name = "GradientStop3Layer" + layer + (isOddIteration ? "Odd" : "");

            // Register a name for each gradient stop with the
            // page so that they can be animated by a storyboard.
            this.RegisterName(gsc0.name, gsc0.stop); // TODO try keeping a list of all these and referencing that based on layer, isOdd, stop index. New object that has these and the targetName and anything else useful.
            this.RegisterName(gsc1.name, gsc1.stop);
            this.RegisterName(gsc2.name, gsc2.stop);
            this.RegisterName(gsc3.name, gsc3.stop);


            //TODO possibly weirdness from adding animations to a storyboard that is already playing?
            if (storyboard != null)
            {
                // We've intentionally skipped index 0 to not animate that gradient stop.
                var oa1 = CreateOffsetAnimation($"GradientStop1Layer{layer}" + (isOddIteration ? "Odd" : ""), TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.03, 0.30);
                var oa2 = CreateOffsetAnimation($"GradientStop2Layer{layer}" + (isOddIteration ? "Odd" : ""), TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.73, 0.37);
                var oa3 = CreateOffsetAnimation($"GradientStop3Layer{layer}" + (isOddIteration ? "Odd" : ""), TimeSpan.FromSeconds(15 + _random.NextDouble() * 15), 0.97, 0.8);
                storyboard.Children.Add(oa1);
                storyboard.Children.Add(oa2);
                storyboard.Children.Add(oa3);
                animations.Add(oa1);
                animations.Add(oa2);
                animations.Add(oa3);
                gsc1.offsetAnimation = oa1;
                gsc2.offsetAnimation = oa2;
                gsc3.offsetAnimation = oa3;


                Action<GradientStopContext> createOpacityOrColorAnim = gsc => 
                {
                    if (false && layer > 0 && _random.NextDouble() > 0.5) // TODO debug, disabled
                    {
                        // opacity anim
                        var opacityOA = CreateOpacityOrColorAnimation(gsc.name, TimeSpan.FromSeconds(15 + _random.NextDouble() * 50), System.Windows.Media.Color.FromScRgb(-1.0F, 0F, 0F, 0F));
                        gsc.colorAnimation = opacityOA;
                        storyboard.Children.Add(opacityOA);
                        animations.Add(opacityOA);
                    }
                    else
                    {
                        //color anim
                        var tweak1 = (float)(0.1 + _random.NextDouble() * 0.25);
                        var tweak2 = (float)(0.1 + _random.NextDouble() * 0.25);
                        var tweak3 = (float)(0.1 + _random.NextDouble() * 0.25);
                        var colorOA = CreateOpacityOrColorAnimation(gsc.name, TimeSpan.FromSeconds(10 + _random.NextDouble() * 40), System.Windows.Media.Color.FromScRgb(0F, tweak1, tweak2, tweak3));
                        gsc.colorAnimation = colorOA;
                        storyboard.Children.Add(colorOA);
                        animations.Add(colorOA);
                    }
                };
                createOpacityOrColorAnim(gsc0);
                createOpacityOrColorAnim(gsc1);
                createOpacityOrColorAnim(gsc2);
                createOpacityOrColorAnim(gsc3);
            }

            AddGSC(gsc0);
            AddGSC(gsc1);
            AddGSC(gsc2);
            AddGSC(gsc3);
            return (gradientBrush, animations);
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

        private void GradientMagic()
        {
            // Create a NameScope for the page so that
            // Storyboards can be used.
            NameScope.SetNameScope(this, new NameScope());

            Storyboard storyboard = new Storyboard();
            var baseRotation = (int)_random.NextInt64(360, 720);
            CreateMagicLayer(0, baseRotation + (int)_random.NextInt64(5, 55), storyboard);
            CreateMagicLayer(1, baseRotation + (int)_random.NextInt64(185, 235), storyboard);
            CreateMagicLayer(2, baseRotation + (int)_random.NextInt64(65, 115), storyboard);
            CreateMagicLayer(3, baseRotation + (int)_random.NextInt64(245, 295), storyboard);
            CreateMagicLayer(4, baseRotation + (int)_random.NextInt64(125, 175), storyboard);
            CreateMagicLayer(5, baseRotation + (int)_random.NextInt64(305, 355), storyboard);

            storyboard.Begin(this);
        }
    }
}
