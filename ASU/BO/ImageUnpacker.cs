﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace ASU.BO
{
    public class ImageUnpacker
    {
        private Bitmap original;
        private object originalLock = new object();
        private int pcComplete = 0;
        private Color? backgroundColour = null;
        private object boxesLock = new object();
        private List<Rectangle> boxes;
        private int areaUnpacked;
        private object areaUnpackedLock = new object();
        private int areaToUnpack;
        private int threadCounter = 0;
        private object threadCompleteCounterLock = new object();
        private int threadCompleteCounter = 0;
        private bool areAllThreadsCreated;
        private bool isUnpackingComplete = false;
        private Size originalSize;
        private System.Drawing.Imaging.ColorPalette pallette = null;
        private bool _isBackgroundColourSet = false;
        private bool _isUnpacking = false;
        private const int INT_MAX_REGION_WIDTH = 400;
        public string FileName;

        public int ColoursCount = 0;
        public event UnpackingCompleteEventHandler UnpackingComplete;
        public delegate void UnpackingCompleteEventHandler();
        public event PcCompleteChangedEventHandler PcCompleteChanged;
        public delegate void PcCompleteChangedEventHandler(int pcComplete);

        public ImageUnpacker(Bitmap image, string fileName)
        {
            if (image.Palette.Entries.Length > 0)
            {
                this.pallette = image.Palette;
            }
            this.original = new Bitmap((Bitmap)image.Clone());
            this.originalSize = image.Size;
            this.boxes = new List<Rectangle>();
            this.FileName = fileName;
        }

        public System.Drawing.Imaging.ColorPalette GetPallette()
        {
            return this.pallette;
        }

        public Size GetSize()
        {
            return this.originalSize;
        }

        public bool IsUnpacking()
        {
            return this._isUnpacking;
        }

        public bool IsBackgroundColourSet()
        {
            return this._isBackgroundColourSet;
        }

        public Color GetBackgroundColour()
        {
            if (this.backgroundColour.HasValue)
            {
                return this.backgroundColour.Value;
            }
            else
            {
                return Color.Black;
            }
        }

        public List<Rectangle> GetBoxes()
        {
            return new List<Rectangle>(this.boxes);
        }

        public bool IsUnpacked()
        {
            return this.isUnpackingComplete;
        }

        public int GetPcComplete()
        {
            return this.pcComplete;
        }

        public Bitmap GetOriginalClone()
        {
            Bitmap clone = default(Bitmap);

            lock ((this.originalLock))
            {
                clone = new Bitmap((Bitmap)this.original.Clone());
            }

            return clone;
        }

        private void SetPcComplete(int pcComplete)
        {
            if (pcComplete > this.pcComplete)
            {
                this.pcComplete = pcComplete;
                if (PcCompleteChanged != null)
                {
                    PcCompleteChanged(this.pcComplete);
                }
            }
        }

        public void StartUnpacking()
        {
            System.Threading.Thread newThread = new System.Threading.Thread(this.Unpack);
            this.isUnpackingComplete = false;
            this.pcComplete = 0;
            this.boxes.Clear();
            this._isUnpacking = true;
            this.threadCounter = 0;
            this.threadCompleteCounter = 0;
            newThread.Start();
        }

        public static List<Rectangle> OrderBoxes(List<Rectangle> boxes, Enums.SelectAllOrder enuSelectAllOrder, Size spriteSheetSize)
        {
            SortedDictionary<int, List<Rectangle>> colOrderedFrames = new SortedDictionary<int, List<Rectangle>>();
            int intLocation = 0;
            List<Rectangle> returnedOrder = new List<Rectangle>();


            foreach (Rectangle objFrame in boxes)
            {
                switch (enuSelectAllOrder)
                {
                    case Enums.SelectAllOrder.TopLeft:
                        intLocation = objFrame.X + (objFrame.Y * spriteSheetSize.Width);
                        break;
                    case Enums.SelectAllOrder.BottomLeft:
                        intLocation = objFrame.X + ((objFrame.Y + objFrame.Height) * spriteSheetSize.Width);
                        break;
                    case Enums.SelectAllOrder.Centre:
                        intLocation = Convert.ToInt32((objFrame.X + (objFrame.Width / 2)) + ((objFrame.Y + (objFrame.Height / 2)) * spriteSheetSize.Width));
                        break;
                }

                if (!colOrderedFrames.ContainsKey(intLocation))
                {
                    colOrderedFrames.Add(intLocation, new List<Rectangle>());
                }
                colOrderedFrames[intLocation].Add(objFrame);
            }

            foreach (int intLocationKey in colOrderedFrames.Keys)
            {
                foreach (Rectangle objFrame in colOrderedFrames[intLocationKey])
                {
                    returnedOrder.Add(objFrame);
                }
            }

            return returnedOrder;
        }

        private void Unpack(object state)
        {
            try
            {
                int intXSize = 0;
                int intYSize = 0;
                Rectangle region = default(Rectangle);
                System.Threading.Thread regionThread = null;

                this.areAllThreadsCreated = false;
                if (!this.backgroundColour.HasValue)
                {
                    this.SetBackgroundColour(this.GetOriginalClone());
                }
                this.SetPcComplete(10);

                intYSize = Convert.ToInt32(Math.Ceiling((double)this.originalSize.Height / 3));
                intXSize = Convert.ToInt32(Math.Ceiling((double)this.originalSize.Width / 3));

                this.areaToUnpack = this.originalSize.Width * this.originalSize.Height;

                for (int y = 0; y <= intYSize * 4; y += intYSize)
                {

                    for (int x = 0; x <= intXSize * 4; x += intXSize)
                    {
                        region = new Rectangle(x, y, Math.Min(intXSize, (this.originalSize.Width - x) - 1), Math.Min(intYSize, (this.originalSize.Height - y) - 1));
                        regionThread = new System.Threading.Thread(this.HandleDividedAreaThread);
                        regionThread.Name = "Region thread " + (y * (intXSize * 4)) + x;
                        this.threadCounter += 1;
                        regionThread.Start(region);

                        this.SetPcComplete(10 + Convert.ToInt32((((Math.Min(y, this.originalSize.Height) * (this.originalSize.Width - 1)) + Math.Min(x, this.originalSize.Width)) / (this.originalSize.Height * this.originalSize.Width)) * 10));
                    }
                }

                this.SetPcComplete(20);
                this.areAllThreadsCreated = true;
                lock ((this.threadCompleteCounterLock))
                {
                    if (this.threadCompleteCounter == this.threadCounter)
                    {
                        this.HandleUnpackComplete();
                    }
                }
            }
            catch (Exception ex)
            {
                ForkandBeard.Logic.ExceptionHandler.HandleException(ex, "cat@forkandbeard.co.uk");
            }
        }

        private void HandleDividedAreaThread(object regionObject)
        {
            try
            {
                this.HandleDividedArea((Rectangle)regionObject, true, this.GetOriginalClone());
            }
            catch (Exception ex)
            {
                ForkandBeard.Logic.ExceptionHandler.HandleException(ex, "cat@forkandbeard.co.uk");
            }
        }


        private void HandleDividedArea(Rectangle region, bool updateCounter, Bitmap image)
        {
            if (region.Width > INT_MAX_REGION_WIDTH || region.Height > INT_MAX_REGION_WIDTH)
            {
                List<Rectangle> quarterRegions = new List<Rectangle>();

                // Top left.
                quarterRegions.Add(new Rectangle(region.X, region.Y, Convert.ToInt32(region.Width / 2), Convert.ToInt32(region.Height / 2)));
                // Top right.
                quarterRegions.Add(new Rectangle(region.X + Convert.ToInt32(region.Width / 2), region.Y, Convert.ToInt32(region.Width / 2) + 1, Convert.ToInt32(region.Height / 2)));
                // Bottom left.
                quarterRegions.Add(new Rectangle(region.X, region.Y + Convert.ToInt32(region.Height / 2), Convert.ToInt32(region.Width / 2), Convert.ToInt32(region.Height / 2) + 1));
                // Bottom right.
                quarterRegions.Add(new Rectangle(region.X + Convert.ToInt32(region.Width / 2), region.Y + Convert.ToInt32(region.Height / 2), Convert.ToInt32(region.Width / 2) + 1, Convert.ToInt32(region.Height / 2) + 1));
                foreach (Rectangle quarter in quarterRegions)
                {
                    this.HandleDividedArea(quarter, false, image);
                }
            }
            else
            {
                using (RegionUnpacker unpacker = new RegionUnpacker(image, region, this.backgroundColour.Value))
                {
                    unpacker.UnpackRegion();
                    lock ((this.areaUnpackedLock))
                    {
                        this.areaUnpacked += Convert.ToInt32((region.Width * region.Height) * 0.8);
                    }

                    this.SetPcComplete(20 + Convert.ToInt32((this.areaUnpacked / this.areaToUnpack) * 75));

                    lock ((this.boxesLock))
                    {
                        this.boxes.AddRange(unpacker.Boxes);
                    }
                }

                lock ((this.areaUnpackedLock))
                {
                    this.areaUnpacked += Convert.ToInt32((region.Width * region.Height) * 0.2);
                }

                this.SetPcComplete(20 + Convert.ToInt32((this.areaUnpacked / this.areaToUnpack) * 80));
            }

            if (updateCounter)
            {
                lock ((this.threadCompleteCounterLock))
                {
                    this.threadCompleteCounter += 1;
                    if (this.areAllThreadsCreated && this.threadCompleteCounter == this.threadCounter)
                    {
                        this.HandleUnpackComplete();
                    }
                }
            }
        }

        private void SetBackgroundColour(Bitmap image)
        {
            Dictionary<Color, int> colours = new Dictionary<Color, int>();
            Color presentColour = default(Color);
            int maxCount = 0;

            for (int x = 0; x <= this.originalSize.Width - 1; x++)
            {
                for (int y = 0; y <= this.originalSize.Height - 1; y++)
                {
                    presentColour = image.GetPixel(x, y);

                    if (!colours.ContainsKey(presentColour))
                    {
                        colours.Add(presentColour, 1);
                    }
                    else
                    {
                        colours[presentColour] += 1;
                    }

                    if ((x + y) % 100 == 0)
                    {
                        int intTotal = 0;

                        intTotal = this.originalSize.Width * this.originalSize.Height;
                        this.SetPcComplete(Convert.ToInt32((((x * (this.originalSize.Height - 1)) + y) / intTotal) * 10));
                    }
                }
            }

            foreach (Color colour in colours.Keys)
            {
                if (colours[colour] >= maxCount)
                {
                    maxCount = colours[colour];
                    this.backgroundColour = colour;
                }
            }
            image.Dispose();
            this._isBackgroundColourSet = true;
            this.ColoursCount = colours.Count - 1;
        }

        private void HandleUnpackComplete()
        {
            using (Bitmap img = this.GetOriginalClone())
            {
                lock ((this.boxesLock))
                {
                    RegionUnpacker.CombineBoxes(ref this.boxes, this.backgroundColour.Value, img);
                }
            }

            this.isUnpackingComplete = true;
            this._isUnpacking = false;
            this.SetPcComplete(100);
            if (UnpackingComplete != null)
            {
                UnpackingComplete();
            }
        }
    }
}