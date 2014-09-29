﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ComicMatrixFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a comic filter to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Photo
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.Runtime.InteropServices;

    using ImageProcessor.Common.Extensions;
    using ImageProcessor.Imaging.Filters.Artistic;

    /// <summary>
    /// Encapsulates methods with which to add a comic filter to an image.
    /// </summary>
    internal class ComicMatrixFilter : MatrixFilterBase
    {
        /// <summary>
        /// Gets the <see cref="T:System.Drawing.Imaging.ColorMatrix"/> for this filter instance.
        /// </summary>
        public override ColorMatrix Matrix
        {
            get { return ColorMatrixes.ComicLow; }
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="image">The current image to process</param>
        /// <param name="newImage">The new Image to return</param>
        /// <returns>
        /// The processed image.
        /// </returns>
        public override Image TransformImage(Image image, Image newImage)
        {
            // Bitmaps for comic pattern
            Bitmap highBitmap = null;
            Bitmap lowBitmap = null;
            Bitmap patternBitmap = null;
            Bitmap edgeBitmap = null;

            try
            {
                using (ImageAttributes attributes = new ImageAttributes())
                {
                    Rectangle rectangle = new Rectangle(0, 0, image.Width, image.Height);

                    attributes.SetColorMatrix(ColorMatrixes.ComicHigh);

                    // Draw the image with the high comic colormatrix.
                    highBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    // Apply a oil painting filter to the image.
                    highBitmap = new OilPaintingFilter(3, 5).ApplyFilter((Bitmap)image);
                    
                    // Draw the edges.
                    edgeBitmap = DrawEdges((Bitmap)image, 120);

                    using (Graphics graphics = Graphics.FromImage(highBitmap))
                    {
                        graphics.DrawImage(highBitmap, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }

                    // Create a bitmap for overlaying.
                    lowBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    // Set the color matrix
                    attributes.SetColorMatrix(this.Matrix);

                    // Draw the image with the losatch colormatrix.
                    using (Graphics graphics = Graphics.FromImage(lowBitmap))
                    {
                        graphics.DrawImage(highBitmap, rectangle, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }

                    // We need to create a new image now with a pattern mask to paint it
                    // onto the other image with.
                    patternBitmap = new Bitmap(rectangle.Width, rectangle.Height, PixelFormat.Format32bppPArgb);

                    // Create the pattern mask.
                    using (Graphics graphics = Graphics.FromImage(patternBitmap))
                    {
                        graphics.Clear(Color.Black);
                        graphics.SmoothingMode = SmoothingMode.HighQuality;

                        for (int y = 0; y < image.Height; y += 8)
                        {
                            for (int x = 0; x < image.Width; x += 4)
                            {
                                graphics.FillEllipse(Brushes.White, x, y, 3, 3);
                                graphics.FillEllipse(Brushes.White, x + 2, y + 4, 3, 3);
                            }
                        }
                    }

                    // Transfer the alpha channel from the mask to the high saturation image.
                    ApplyMask(patternBitmap, lowBitmap);

                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        graphics.Clear(Color.Transparent);

                        // Overlay the image.
                        graphics.DrawImage(highBitmap, 0, 0);
                        graphics.DrawImage(lowBitmap, 0, 0);
                        graphics.DrawImage(edgeBitmap, 0, 0);

                        // Draw an edge around the image.
                        using (Pen blackPen = new Pen(Color.Black))
                        {
                            blackPen.Width = 4;
                            graphics.DrawRectangle(blackPen, rectangle);
                        }

                        // Dispose of the other images
                        highBitmap.Dispose();
                        lowBitmap.Dispose();
                        patternBitmap.Dispose();
                        edgeBitmap.Dispose();
                    }
                }

                // Reassign the image.
                image.Dispose();
                image = newImage;
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                if (highBitmap != null)
                {
                    highBitmap.Dispose();
                }

                if (lowBitmap != null)
                {
                    lowBitmap.Dispose();
                }

                if (patternBitmap != null)
                {
                    patternBitmap.Dispose();
                }

                if (edgeBitmap != null)
                {
                    edgeBitmap.Dispose();
                }
            }

            return image;
        }

        /// <summary>
        /// Detects and draws edges.
        /// TODO: Move this to another class and do edge detection.
        /// </summary>
        /// <param name="sourceBitmap">
        /// The source bitmap.
        /// </param>
        /// <param name="threshold">
        /// The threshold.
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/>.
        /// </returns>
        private static Bitmap DrawEdges(Bitmap sourceBitmap, byte threshold = 0)
        {
            Color color = Color.Black;
            int width = sourceBitmap.Width;
            int height = sourceBitmap.Height;

            BitmapData sourceData = sourceBitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            int strideWidth = sourceData.Stride;
            int scanHeight = sourceData.Height;

            int bufferSize = strideWidth * scanHeight;
            byte[] pixelBuffer = new byte[bufferSize];
            byte[] resultBuffer = new byte[bufferSize];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);

            sourceBitmap.UnlockBits(sourceData);

            for (int offsetY = 1; offsetY < height - 1; offsetY++)
            {
                for (int offsetX = 1; offsetX < width - 1; offsetX++)
                {
                    int byteOffset = (offsetY * strideWidth) + (offsetX * 4);

                    int blueGradient = Math.Abs(pixelBuffer[byteOffset - 4] - pixelBuffer[byteOffset + 4]);

                    blueGradient +=
                        Math.Abs(
                            pixelBuffer[byteOffset - strideWidth] - pixelBuffer[byteOffset + strideWidth]);

                    byteOffset++;

                    int greenGradient = Math.Abs(pixelBuffer[byteOffset - 4] - pixelBuffer[byteOffset + 4]);

                    greenGradient +=
                        Math.Abs(
                            pixelBuffer[byteOffset - strideWidth] - pixelBuffer[byteOffset + strideWidth]);

                    byteOffset++;

                    int redGradient = Math.Abs(pixelBuffer[byteOffset - 4] - pixelBuffer[byteOffset + 4]);

                    redGradient +=
                        Math.Abs(
                            pixelBuffer[byteOffset - strideWidth] - pixelBuffer[byteOffset + strideWidth]);

                    bool exceedsThreshold;
                    if (blueGradient + greenGradient + redGradient > threshold)
                    {
                        exceedsThreshold = true;
                    }
                    else
                    {
                        byteOffset -= 2;

                        blueGradient = Math.Abs(pixelBuffer[byteOffset - 4] - pixelBuffer[byteOffset + 4]);
                        byteOffset++;

                        greenGradient = Math.Abs(pixelBuffer[byteOffset - 4] - pixelBuffer[byteOffset + 4]);
                        byteOffset++;

                        redGradient = Math.Abs(pixelBuffer[byteOffset - 4] - pixelBuffer[byteOffset + 4]);

                        if (blueGradient + greenGradient + redGradient > threshold)
                        {
                            exceedsThreshold = true;
                        }
                        else
                        {
                            byteOffset -= 2;

                            blueGradient =
                                Math.Abs(pixelBuffer[byteOffset - strideWidth] - pixelBuffer[byteOffset + strideWidth]);

                            byteOffset++;

                            greenGradient =
                                Math.Abs(pixelBuffer[byteOffset - strideWidth] - pixelBuffer[byteOffset + strideWidth]);

                            byteOffset++;

                            redGradient =
                                Math.Abs(pixelBuffer[byteOffset - strideWidth] - pixelBuffer[byteOffset + strideWidth]);

                            if (blueGradient + greenGradient + redGradient > threshold)
                            {
                                exceedsThreshold = true;
                            }
                            else
                            {
                                byteOffset -= 2;

                                blueGradient =
                                    Math.Abs(
                                        pixelBuffer[byteOffset - 4 - strideWidth]
                                        - pixelBuffer[byteOffset + 4 + strideWidth]);

                                blueGradient +=
                                    Math.Abs(
                                        pixelBuffer[byteOffset - strideWidth + 4]
                                        - pixelBuffer[byteOffset + strideWidth - 4]);

                                byteOffset++;

                                greenGradient =
                                    Math.Abs(
                                        pixelBuffer[byteOffset - 4 - strideWidth]
                                        - pixelBuffer[byteOffset + 4 + strideWidth]);

                                greenGradient +=
                                    Math.Abs(
                                        pixelBuffer[byteOffset - strideWidth + 4]
                                        - pixelBuffer[byteOffset + strideWidth - 4]);

                                byteOffset++;

                                redGradient =
                                    Math.Abs(
                                        pixelBuffer[byteOffset - 4 - strideWidth]
                                        - pixelBuffer[byteOffset + 4 + strideWidth]);

                                redGradient +=
                                    Math.Abs(
                                        pixelBuffer[byteOffset - strideWidth + 4]
                                        - pixelBuffer[byteOffset + strideWidth - 4]);

                                exceedsThreshold = blueGradient + greenGradient + redGradient > threshold;
                            }
                        }
                    }

                    byteOffset -= 2;

                    double blue;
                    double red;
                    double green;
                    double alpha;
                    if (exceedsThreshold)
                    {
                        blue = color.B; // 0;
                        green = color.G; // 0;
                        red = color.R; // 0;
                        alpha = 255;
                    }
                    else
                    {
                        // These would normally be used to transfer the correct value across.
                        // blue = pixelBuffer[byteOffset];
                        // green = pixelBuffer[byteOffset + 1];
                        // red = pixelBuffer[byteOffset + 2];
                        blue = 255;
                        green = 255;
                        red = 255;
                        alpha = 0;
                    }

                    resultBuffer[byteOffset] = blue.ToByte();
                    resultBuffer[byteOffset + 1] = green.ToByte();
                    resultBuffer[byteOffset + 2] = red.ToByte();
                    resultBuffer[byteOffset + 3] = alpha.ToByte();
                }
            }

            Bitmap resultBitmap = new Bitmap(width, height);

            BitmapData resultData = resultBitmap.LockBits(
                new Rectangle(0, 0, resultBitmap.Width, resultBitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);

            resultBitmap.UnlockBits(resultData);
            return resultBitmap;
        }

        /// <summary>
        /// Applies a mask .
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="destination">
        /// The destination.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if the two images are of different size.
        /// </exception>
        private static void ApplyMask(Bitmap source, Bitmap destination)
        {
            if (source.Size != destination.Size)
            {
                throw new ArgumentException();
            }

            using (FastBitmap sourceBitmap = new FastBitmap(source))
            {
                using (FastBitmap destinationBitmap = new FastBitmap(destination))
                {
                    int width = source.Width;
                    int height = source.Height;

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            Color sourceColor = sourceBitmap.GetPixel(x, y);
                            Color destinationColor = destinationBitmap.GetPixel(x, y);

                            if (destinationColor.A != 0)
                            {
                                destinationBitmap.SetPixel(x, y, Color.FromArgb(sourceColor.B, destinationColor.R, destinationColor.G, destinationColor.B));
                            }
                        }
                    }
                }
            }
        }
    }
}