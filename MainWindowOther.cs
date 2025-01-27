using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using System.Windows.Media.Animation;
namespace PalletCheck
{
    public partial class MainWindow : Window
    {
        public enum MessageState
        {
            Critical,
            Warning,
            Normal,
            Notice
        }

        public static void UpdateTextBlock(TextBlock textBlock, string message, MessageState state)
        {
            if (textBlock == null)
                throw new ArgumentNullException(nameof(textBlock));

            textBlock.Dispatcher.Invoke(() =>
            {
                // Get the current time and format it as a string
                string timestamp = DateTime.Now.ToString("MM-dd-yyyy HH:mm:ss");
                // Combine the timestamp with the message
                textBlock.Text = $"[{timestamp}] {message}";

                // Set the font color
                switch (state)
                {
                    case MessageState.Critical:
                        textBlock.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                    case MessageState.Warning:
                        textBlock.Foreground = new SolidColorBrush(Colors.Yellow);
                        break;
                    case MessageState.Notice:
                        textBlock.Foreground = new SolidColorBrush(Colors.Green);
                        break;
                    case MessageState.Normal:
                    default:
                        textBlock.Foreground = new SolidColorBrush(Colors.White);
                        break;
                }

                // Add an animation effect to the TextBlock
                var transform = new TranslateTransform();
                textBlock.RenderTransform = transform;

                // Create an animation that moves from 50 pixels below to the current position
                var animation = new DoubleAnimation
                {
                    From = 50, // Starting position
                    To = 0,    // Ending position
                    Duration = TimeSpan.FromSeconds(1), // Animation duration
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } // Easing effect
                };

                // Start the animation
                transform.BeginAnimation(TranslateTransform.YProperty, animation);
            });
        }

        public void UpdateTextBlock(TextBlock textBlock, string message)
        {
            if (textBlock == null)
                throw new ArgumentNullException(nameof(textBlock));

            textBlock.Dispatcher.Invoke(() =>
            {
                // Only set the message content
                textBlock.Text = message;

                // Add an animation effect to the TextBlock
                var transform = new TranslateTransform();
                textBlock.RenderTransform = transform;

                // Create an animation that moves from 50 pixels below to the current position
                var animation = new DoubleAnimation
                {
                    From = 50, // Starting position
                    To = 0,    // Ending position
                    Duration = TimeSpan.FromSeconds(1), // Animation duration
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } // Easing effect
                };

                // Start the animation
                transform.BeginAnimation(TranslateTransform.YProperty, animation);
            });
        }

        public void UpdateTextBlock(TextBlock textBlock, string message, Color color, double fontSize)
        {
            if (textBlock == null)
                throw new ArgumentNullException(nameof(textBlock));

            textBlock.Dispatcher.Invoke(() =>
            {
                // Set the message content
                textBlock.Text = message;

                // Set the color and font size
                textBlock.Foreground = new SolidColorBrush(color);
                textBlock.FontSize = fontSize;

                // Add an animation effect to the TextBlock
                var transform = new TranslateTransform();
                textBlock.RenderTransform = transform;

                // Create an animation that moves from 50 pixels below to the current position
                var animation = new DoubleAnimation
                {
                    From = 50, // Starting position
                    To = 0,    // Ending position
                    Duration = TimeSpan.FromSeconds(1), // Animation duration
                    EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut } // Easing effect
                };

                // Start the animation
                transform.BeginAnimation(TranslateTransform.YProperty, animation);
            });
        }

        public void UpdateDigitalClock(TextBlock clockTextBlock, bool colonVisible)
        {
            if (clockTextBlock == null)
                throw new ArgumentNullException(nameof(clockTextBlock));

            // Get the current time
            DateTime now = DateTime.Now;

            // Implement blinking colon: controlled by the external parameter
            string colon = colonVisible ? ":" : " ";

            // American date format: MM/DD/YYYY
            string date = now.ToString("MM/dd/yyyy");

            // Time format: HH:mm:ss (24-hour format)
            string time = $"{now:HH}{colon}{now:mm}{colon}{now:ss}";

            // Set the TextBlock content, including date and time
            clockTextBlock.Text = $"{date} {time}";

            // Set the style, only needs to be set once
            clockTextBlock.FontSize = 24; // Font size, reduced slightly to fit both date and time
            clockTextBlock.FontFamily = new FontFamily("Courier New"); // Monospaced font
            clockTextBlock.Foreground = new SolidColorBrush(Colors.LimeGreen); // Green font color
            clockTextBlock.Background = new SolidColorBrush(Colors.Black); // Black background
            clockTextBlock.TextAlignment = TextAlignment.Center; // Center alignment
        }

    }
}
