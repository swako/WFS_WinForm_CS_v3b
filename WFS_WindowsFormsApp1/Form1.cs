namespace WFS_WindowsFormsApp1_x86
{

    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Thorlabs.WFS.Interop;

    public partial class Form1 : Form
    {
        #region Defines

        private static int sampleCameraResolWfs = 2;


        // 0    1280x1024 pixels          
        // 1    1024x1024          
        // 2     768x768            
        // 3     512x512            
        // 4     320x320
        

        private static int sampleCameraResolWfs10 = 2; // CAM_RES_WFS10_360 = 360x360 pixels
        private static int sampleCameraResolWfs20 = 3; // CAM_RES_WFS20_512 = 512x512 pixels
        private static int sampleCameraResolWfs30 = 3; // CAM_RES_WFS30_512 = 512x512 pixels
        private static int sampleCameraResolWfs40 = 3; // CAM_RES_WFS40_512 = 512x512 pixels

        private static int pixelFormat = 0; // PIXEL_FORMAT_MONO8 = 0

        //private static int sampleRefPlane = 0;
        //WFS_REF_INTERNAL = 0
        //WFS_REF_USER = 1


        private static double samplePupilCentroidX = 0.0; // in mm
        private static double samplePupilCentroidY = 0.0;

        private static double samplePupilDiameterX = 3.0; // in mm, needs to fit to selected camera resolution
        private static double samplePupilDiameterY = 3.0;
        private static int sampleImageReadings = 10; // trials to read a exposed spotfield image 
        private static int sampleOptionDynNoiseCut = 1; // use dynamic noise cut features
        private static int sampleOptionCalcSpotDias = 0; // 0: don't calculate spot diameters
        private static int sampleOptionCancelTilt = 1; // cancel average wavefront tip and tilt
        private static int sampleOptionLimitToPupil = 0; // 0: don't limit wavefront calculation to pupil interior
        private static int sampleZernikeOrders = 3; // calculate up to 3rd Zernike order
        private static int maxZernikeModes = 66; // allocate Zernike array of 67 because index is 1..66
        private static int sampleOptionHighspeed = 1; // use highspeed mode (only for WFS10 and WFS20 instruments)
        private static int sampleOptionHsAdaptCentr = 1; // adapt centroids in highspeed mode to previously measured centroids
        private static int sampleHsNoiseLevel = 30; // cut lower 30 digits in highspeed mode
        private static int sampleOptionHsAllowAutoexpos = 1; // allow autoexposure in highspeed mode (runs somewhat slower)

        private static int sampleWavefrontType = 0; // WAVEFRONT_MEAS = 0

        private static int samplePrintoutSpots = 3; // printout results for first 3 x 3 spots only

        #endregion

        #region Fields
        private static WFS instrument = new WFS(IntPtr.Zero);
        #endregion

        private int status;

        public Form1()
        {
            this.InitializeComponent();
            int selectedInstrId = 0;
            string resourceName = default(string);

            Console.WriteLine("==============================================================");
            Console.WriteLine(" Thorlabs instrument driver sample for WFS series instruments");
            Console.WriteLine("==============================================================");

            StringBuilder wfsDriverRev = new StringBuilder(WFS.BufferSize);
            StringBuilder camDriverRevision = new StringBuilder(WFS.BufferSize);
            instrument.revision_query(wfsDriverRev, camDriverRevision);
            Console.WriteLine();
            Console.Write("WFS instrument driver version : ");
            Console.WriteLine(wfsDriverRev.ToString(), camDriverRevision.ToString());
            Console.WriteLine();

            // implementation with driver functions
            this.SelectInstrument(out selectedInstrId, out resourceName);

            instrument = new WFS(resourceName, false, false);


            this.SelectMla();

            StringBuilder manufacturerName = new StringBuilder(WFS.BufferSize);
            StringBuilder instrumentName = new StringBuilder(WFS.BufferSize);
            StringBuilder serialNumberWfs = new StringBuilder(WFS.BufferSize);
            StringBuilder serialNumberCam = new StringBuilder(WFS.BufferSize);

            if (0 == instrument.GetInstrumentInfo(manufacturerName, instrumentName, serialNumberWfs, serialNumberCam))
            {
                this.textBox_instrumentName.Text = instrumentName.ToString();
                this.textBox_serialNumberCam.Text = serialNumberCam.ToString();

            }


            // Configure the camera resolution to a pre-defined setting
            Console.WriteLine("\n>> Configure Device, use pre-defined settings <<");
            this.ConfigureDevice(selectedInstrId);
            //---



            this.DefinePupil();
            this.textBox_PupilDiameterX.Text = samplePupilDiameterX.ToString();
                 
              

            Console.WriteLine("\nWFS instrument is ready.");


        }



        private void SelectInstrument(out int selectedInstrId, out string resourceName)
        {
            resourceName = null;
            int instrCount;
            instrument.GetInstrumentListLen(out instrCount);
            if (0 == instrCount)
            {
                Console.WriteLine("No Wavefront Sensor instrument found!");
                selectedInstrId = -1;
                ConsoleKeyInfo waitKey = Console.ReadKey(true);
                return;
            }

            Console.WriteLine("Available Wavefront Sensor instruments:\n");
            int deviceID;
            int inUse;

            StringBuilder instrumentName = new StringBuilder(WFS.BufferSize);
            StringBuilder instrumentSN = new StringBuilder(WFS.BufferSize);
            StringBuilder resourceNameTemp = new StringBuilder(WFS.BufferSize);

            for (int i = 0; i < instrCount; ++i)
            {
                instrument.GetInstrumentListInfo(i, out deviceID, out inUse, instrumentName, instrumentSN, resourceNameTemp);
                resourceName = resourceNameTemp.ToString();
                Console.Write(deviceID.ToString() + "  " + instrumentName.ToString() + "  " + instrumentSN.ToString() + ((0 == inUse) ? "" : "  (inUse)") + "\n\n");
            }

            Console.Write("Select a Wavefront Sensor instrument: ");

            string input = 1.ToString();
            bool chk = Int32.TryParse(input, out selectedInstrId);
            if (!chk)
            {
                throw new Exception("Invalid selection", new Exception(input.ToString()));
            }

            // get selected resource name
            int deviceIDtemp = 0;
            for (int i = 0; (i < instrCount) && (deviceIDtemp != selectedInstrId); ++i)
            {
                instrument.GetInstrumentListInfo(i, out deviceID, out inUse, instrumentName, instrumentSN, resourceNameTemp);
                deviceIDtemp = deviceID;
                resourceName = resourceNameTemp.ToString();
            }

            // selectedInstrId available?
            if (deviceIDtemp != selectedInstrId)
            {
                throw new Exception("Invalid selection", new Exception(selectedInstrId.ToString()));
            }
        }

        private void SelectMla()
        {
            int selectedMla;
            int mlaCount;
            instrument.GetMlaCount(out mlaCount);

            Console.WriteLine("\nAvailable Microlens Arrays:\n");
            StringBuilder mlaName = new StringBuilder(WFS.BufferSize);
            double camPitch;
            double lensletPitch;
            double spotOffsetX;
            double spotOffsetY;
            double lensletFum;
            double grdCorr0;
            double grdCorr45;
            for (int i = 0; i < mlaCount; ++i)
            {
                instrument.GetMlaData(i, mlaName, out camPitch, out lensletPitch, out spotOffsetX, out spotOffsetY, out lensletFum, out grdCorr0, out grdCorr45);
                Console.Write(i.ToString() + "  " + mlaName.ToString() + "  " + camPitch.ToString() + "  " + lensletPitch.ToString() + "\n");
            }

            Console.Write("\nSelect a Microlens Array: ");
            string input = 0.ToString();
            bool chk = Int32.TryParse(input, out selectedMla);
            if (!chk)
            {
                throw new Exception("Invalid selection", new Exception(input.ToString()));
            }
            else
            {
                if ((0 > selectedMla) || (mlaCount <= selectedMla))
                {
                    throw new Exception("Invalid selection", new Exception(selectedMla.ToString()));
                }
                else
                {
                    instrument.SelectMla(selectedMla);
                }
            }
        }


        private void instrument_LoadUserRefFile()
        {
            instrument.LoadUserRefFile();
        }



    private void ConfigureDevice(int selectedInstrId)

    /// <summary>
    /// set the camera to a pre-defined resolution (pixels x pixels)
    /// this image size needs to fit the beam size and pupil size
    /// </summary>


    {
        int spotsX = 0;
        int spotsY = 0;
        if ((0 == (selectedInstrId & WFS.DeviceOffsetWFS10)) &&
            (0 == (selectedInstrId & WFS.DeviceOffsetWFS20)) &&
            (0 == (selectedInstrId & WFS.DeviceOffsetWFS30)) &&
            (0 == (selectedInstrId & WFS.DeviceOffsetWFS40)))
        {
            Console.Write("Configure WFS camera with resolution index: " + sampleCameraResolWfs.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs].ToString() + " pixels)\n");
            instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs, out spotsX, out spotsY);
        }
        else
        {
            if (0 != (selectedInstrId & WFS.DeviceOffsetWFS10)) // WFS10 instrument
            {
                Console.Write("Configure WFS10 camera with resolution index: " + sampleCameraResolWfs10.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs10].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs10].ToString() + " pixels)\n");
                instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs10, out spotsX, out spotsY);
            }
            if (0 != (selectedInstrId & WFS.DeviceOffsetWFS20)) // WFS20 instrument
            {
                Console.Write("Configure WFS20 camera with resolution index: " + sampleCameraResolWfs20.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs20].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs20].ToString() + " pixels)\n");
                instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs20, out spotsX, out spotsY);
            }
            if (0 != (selectedInstrId & WFS.DeviceOffsetWFS30)) // WFS30 instrument
            {
                Console.Write("Configure WFS30 camera with resolution index: " + sampleCameraResolWfs30.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs30].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs30].ToString() + " pixels)\n");
                instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs30, out spotsX, out spotsY);
            }
            if (0 != (selectedInstrId & WFS.DeviceOffsetWFS40)) // WFS40 instrument
            {
                Console.Write("Configure WFS40 camera with resolution index: " + sampleCameraResolWfs40.ToString() + " (" + WFS.CamWFSXPixel[sampleCameraResolWfs40].ToString() + " x " + WFS.CamWFSYPixel[sampleCameraResolWfs40].ToString() + " pixels)\n");
                instrument.ConfigureCam(pixelFormat, sampleCameraResolWfs40, out spotsX, out spotsY);
            }
        }
        Console.WriteLine("Camera is configured to detect " + spotsX.ToString() + " x " + spotsY.ToString() + " lenslet spots.\n", spotsX, spotsY);
    }


    private void DefinePupil()

    /// <summary>
    /// Set the pupil to pre-defined values, the pupil needs to fit the selected camera resolution and the beam diameter
    /// Zernike results depend and relate to the pupil size!
    /// </summary>
    /// 
       
    {
        Console.WriteLine("\nDefine pupil to:");
        Console.WriteLine("Centroid_x = " + samplePupilCentroidX.ToString("F3"));
        Console.WriteLine("Centroid_y = " + samplePupilCentroidY.ToString("F3"));
        Console.WriteLine("Diameter_x = " + samplePupilDiameterX.ToString("F3"));
        Console.WriteLine("Diameter_y = " + samplePupilDiameterY.ToString("F3") + "\n");

            instrument.SetPupil(samplePupilCentroidX, samplePupilCentroidY, samplePupilDiameterX, samplePupilDiameterY);

            // SetPupil において、
            // 円の場合なら、samplePupilDiameterXとY のどちらも 3 mm にする。
            // 光軸が中心にあってるなら、samplePupilCentroidXとYのどちらも 0 mm にする。

        }

    private void AdjustImageBrightness()
    {
        ConsoleKeyInfo waitKey;
        int status;
        int expos_ok = 0;
        double exposAct;
        double masterGainAct;

        Console.WriteLine("\nRead camera images:\n");
        Console.WriteLine("Image No.     Status     ->   newExposure[ms]   newGainFactor");

        for (int cnt = 0; cnt < sampleImageReadings; ++cnt)
        {
            // take a camera image with auto exposure, note that there may several function calls required to get an optimal exposed image

            instrument.TakeSpotfieldImageAutoExpos(out exposAct, out masterGainAct);
            Console.Write("    " + cnt.ToString() + "     ");

            // check instrument status for non-optimal image exposure
            instrument.GetStatus(out status);

            if (0 != (status & WFS.StatBitHighPower))
            {
                Console.Write("Power too high!    ");
            }
            else if (0 != (status & WFS.StatBitLowPower))
            {
                Console.Write("Power too low!     ");
            }
            else if (0 != (status & WFS.StatBitHighAmbientLight))
            {
                Console.Write("High ambient light!");
            }
            else
            {
                Console.Write("OK                 ");
            }
            Console.WriteLine("     " + exposAct.ToString("F3") + "          " + masterGainAct.ToString("F3"));

            if ((0 == (status & WFS.StatBitHighPower)) &&
                (0 == (status & WFS.StatBitLowPower)) &&
                (0 == (status & WFS.StatBitHighAmbientLight)))
            {
                expos_ok = 1;
                break; // image well exposed and is usable
            }
        }

        // close program if no well exposed image is feasible
        if (0 == expos_ok)
        {
            Console.Write("\nSample program will be closed because of unusable image quality, press <ANY_KEY>.");
            instrument.Dispose(); // required to release allocated driver data
            waitKey = Console.ReadKey(true);
            throw new Exception("Unusable Image");
        }
    }

    private void CalcSpotCentroids()
    {
        instrument.CalcSpotsCentrDiaIntens(sampleOptionDynNoiseCut, sampleOptionCalcSpotDias);

        // get centroid result arrays
        float[,] centroidX = new float[WFS.MaxSpotY, WFS.MaxSpotX];
        float[,] centroidY = new float[WFS.MaxSpotY, WFS.MaxSpotX];

        instrument.GetSpotCentroids(centroidX, centroidY);

        // print out some centroid positions
        Console.WriteLine("\nCentroid X Positions in pixels (first 5x5 elements)\n");
        for (int i = 0; i < samplePrintoutSpots; ++i)
        {
            for (int j = 0; j < samplePrintoutSpots; ++j)
            {
                Console.Write(" " + centroidX[i, j].ToString("F3"));
            }
            Console.WriteLine("");
        }

        Console.WriteLine("\nCentroid Y Positions in pixels (first 5x5 elements)\n");
        for (int i = 0; i < samplePrintoutSpots; ++i)
        {
            for (int j = 0; j < samplePrintoutSpots; ++j)
            {
                Console.Write("  " + centroidY[i, j].ToString("F3"));
            }
            Console.WriteLine("");
        }
    }

    private void GetSpotfieldImage()
    {
        byte[] imageBuffer = new byte[WFS.ImageBufferSize];
        int rows;
        int cols;
        instrument.GetSpotfieldImage(imageBuffer, out rows, out cols);

            Console.Write("\n");

            Console.Write("row = " + rows.ToString());
            Console.Write("\n");
            Console.Write("column = " + cols.ToString());

            Console.Write("\n");

            Console.Write("imagebuffer = " + imageBuffer.ToString());

            Console.Write("\n");
           
        }

    private void CalcBeamCentroid()
    {
        double beamCentroidX, beamCentroidY, beamDiameterX, beamDiameterY;
        instrument.CalcBeamCentroidDia(out beamCentroidX, out beamCentroidY, out beamDiameterX, out beamDiameterY);

        Console.WriteLine("\nInput beam is measured to:");
        Console.WriteLine("CentroidX = " + beamCentroidX.ToString("F3") + " mm");
        Console.WriteLine("CentroidY = " + beamCentroidY.ToString("F3") + " mm");
        Console.WriteLine("DiameterX = " + beamDiameterX.ToString("F3") + " mm");
        Console.WriteLine("DiameterY = " + beamDiameterY.ToString("F3") + " mm");
    }

    private void CalcSpotDeviations()
    {
        instrument.CalcSpotToReferenceDeviations(sampleOptionCancelTilt);

        // get spot deviations
        float[,] deviationX = new float[WFS.MaxSpotY, WFS.MaxSpotX];
        float[,] deviationY = new float[WFS.MaxSpotY, WFS.MaxSpotX];
        instrument.GetSpotDeviations(deviationX, deviationY);

        // print out some spot deviations
        Console.WriteLine("\nSpot Deviation X in pixels (first 5x5 elements)\n");
        for (int i = 0; i < samplePrintoutSpots; ++i)
        {
            for (int j = 0; j < samplePrintoutSpots; ++j)
            {
                Console.Write(" " + deviationX[i, j].ToString("F3"));
            }
            Console.WriteLine("");
        }
        Console.WriteLine("\nSpot Deviation Y in pixels (first 5x5 elements)\n");
        for (int i = 0; i < samplePrintoutSpots; ++i)
        {
            for (int j = 0; j < samplePrintoutSpots; ++j)
            {
                Console.Write(" " + deviationY[i, j].ToString("F3"));
            }
            Console.WriteLine("");
        }
    }

    private void CalcWavefront()
    {

        //            ConsoleKeyInfo waitKey;

        float[,] wavefront = new float[WFS.MaxSpotY, WFS.MaxSpotX];

        instrument.CalcWavefront(sampleWavefrontType, sampleOptionLimitToPupil, wavefront);

        // print out some wavefront points
        Console.WriteLine("\nWavefront in microns (first 5x5 elements)\n");

        for (int i = 0; i < samplePrintoutSpots; ++i)
        {
            for (int j = 0; j < samplePrintoutSpots; ++j)
            {
                Console.Write(" " + wavefront[i, j].ToString("F3"));
            }
            Console.WriteLine("");
        }


            //Console.WriteLine("\nPress <ANY_KEY> to proceed...");
        //waitKey = Console.ReadKey(true);

        // calculate wavefront statistics within defined pupil
        double wavefrontMin, wavefrontMax, wavefrontDiff, wavefrontMean, wavefrontRms, wavefrontWeightedRms;
        instrument.CalcWavefrontStatistics(out wavefrontMin, out wavefrontMax, out wavefrontDiff, out wavefrontMean, out wavefrontRms, out wavefrontWeightedRms);

        Console.WriteLine("\nWavefront Statistics in microns:");
        Console.WriteLine("Min          : " + wavefrontMin.ToString("F3"));
        Console.WriteLine("Max          : " + wavefrontMax.ToString("F3"));
        Console.WriteLine("Diff         : " + wavefrontDiff.ToString("F3"));
        Console.WriteLine("Mean         : " + wavefrontMean.ToString("F3"));
        Console.WriteLine("RMS          : " + wavefrontRms.ToString("F3"));
        Console.WriteLine("Weigthed RMS : " + wavefrontWeightedRms.ToString("F3"));
    }

    private void CalcZernikes()
    {
        Console.WriteLine("\nZernike fit up to order " + sampleZernikeOrders.ToString());
        int zernike_order = sampleZernikeOrders;
        float[] zernikeUm = new float[maxZernikeModes + 1];
        float[] zernikeOrdersRmsUm = new float[maxZernikeModes + 1];
        double rocMm;
        instrument.ZernikeLsf(out zernike_order, zernikeUm, zernikeOrdersRmsUm, out rocMm); // also calculates deviation from centroid data for wavefront integration

        Console.WriteLine("\nZernike Mode    Coefficient");
        for (int i = 0; i < WFS.ZernikeModes[sampleZernikeOrders]; ++i)
        {
            Console.WriteLine("  " + i.ToString() + "             " + zernikeUm[i].ToString("F3"));
        }
    }

    private void HighspeedMode(int selectedInstrId)

    {
        ///// <summary>
        ///// enter highspeed mode for WFS10 and WFS20 instruments only
        ///// in this mode the camera itself calculates the spot centroids
        ///// this enables much faster measurements
        ///// </summary>


        ConsoleKeyInfo waitKey;
        if ((0 != (selectedInstrId & WFS.DeviceOffsetWFS10)) ||
            (0 != (selectedInstrId & WFS.DeviceOffsetWFS20))) // for WFS10 or WFS20 instrument only
        {
            Console.Write("\nEnter Highspeed Mode 0/1? ");
            string input = Console.ReadLine();
            int selection;
            bool chk = Int32.TryParse(input.ToString(), out selection);
            if (!chk)
            {
                throw new Exception("Invalid selection", new Exception(input.ToString()));
            }
            else
            {
                if ((0 != selection) && (1 != selection))
                {
                    throw new Exception("Invalid selection", new Exception(selection.ToString()));
                }
            }

            if (1 == selection)
            {
                // set highspeed mode active, use pre-defined options, refere to WFS_SetHighspeedMode() function help
                instrument.SetHighspeedMode(sampleOptionHighspeed, sampleOptionHsAdaptCentr, sampleHsNoiseLevel, sampleOptionHsAllowAutoexpos);
                int hsWinCountX, hsWinCountY, hsWinSizeX, hsWinSizeY;
                int[] hsWinStartX = new int[WFS.MaxSpotX];
                int[] hsWinStartY = new int[WFS.MaxSpotY];

                instrument.GetHighspeedWindows(out hsWinCountX, out hsWinCountY, out hsWinSizeX, out hsWinSizeY, hsWinStartX, hsWinStartY);

                Console.WriteLine("\nCentroid detection windows are defined as follows:\n"); // refere to WFS_GetHighspeedWindows function help
                Console.WriteLine("CountX = " + hsWinCountX.ToString() + ", CountY = " + hsWinCountY.ToString());
                Console.WriteLine("SizeX  = " + hsWinSizeX.ToString() + ", SizeY  = " + hsWinSizeY.ToString());
                Console.WriteLine("Start coordinates x: ");
                for (int i = 0; i < hsWinCountX; ++i)
                {
                    Console.Write(hsWinStartX[i].ToString() + " ");
                }
                Console.WriteLine("\n");
                Console.WriteLine("Start coordinates y: ");
                for (int i = 0; i < hsWinCountY; ++i)
                {
                    Console.Write(hsWinStartY[i].ToString() + " ");
                }
                Console.WriteLine("\n");

                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(false);

                double exposAct;
                double masterGainAct;
                // take a camera image with auto exposure, this is also supported in highspeed-mode
                instrument.TakeSpotfieldImageAutoExpos(out exposAct, out masterGainAct);
                Console.WriteLine("\nexposure = " + exposAct.ToString("F3") + " ms, gain =  " + masterGainAct.ToString("F3") + "\n");

                double beamCentroidX;
                double beamCentroidY;
                double beamDiameterX;
                double beamDiameterY;
                // get centroid and diameter of the optical beam, these data are based on the detected centroids
                instrument.CalcBeamCentroidDia(out beamCentroidX, out beamCentroidY, out beamDiameterX, out beamDiameterY);
                Console.WriteLine("\nInput beam is measured to:\n");
                Console.WriteLine("CentroidX = " + beamCentroidX.ToString("F3") + " mm");
                Console.WriteLine("CentroidY = " + beamCentroidY.ToString("F3") + " mm");
                Console.WriteLine("DiameterX = " + beamDiameterX.ToString("F3") + " mm");
                Console.WriteLine("DiameterY = " + beamDiameterY.ToString("F3") + " mm");

                Console.WriteLine("\nPress <ANY_KEY> to proceed...");
                waitKey = Console.ReadKey(false);

                // Info: calling WFS_CalcSpotsCentrDiaIntens() is not required because the WFS10/WFS20 camera itself already did the calculation

                float[,] centroidX = new float[WFS.MaxSpotY, WFS.MaxSpotX];
                float[,] centroidY = new float[WFS.MaxSpotY, WFS.MaxSpotX];
                // get centroid result arrays
                instrument.GetSpotCentroids(centroidX, centroidY);

                // print out some centroid positions
                Console.WriteLine("\nCentroid X Positions in pixels (first 5x5 elements)\n");
                for (int i = 0; i < samplePrintoutSpots; ++i)
                {
                    for (int j = 0; j < samplePrintoutSpots; ++j)
                    {
                        Console.Write(" " + centroidX[i, j].ToString("F3"));
                    }
                    Console.WriteLine("");
                }

                Console.WriteLine("\nCentroid Y Positions in pixels (first 5x5 elements)\n");
                for (int i = 0; i < samplePrintoutSpots; ++i)
                {
                    for (int j = 0; j < samplePrintoutSpots; ++j)
                    {
                        Console.Write(" " + centroidY[i, j].ToString("F3"));
                    }
                    Console.WriteLine("");
                }

            }

        }
    }

     private void Form1_Load(object sender, EventArgs e)
        {

        }

      private void Form1_closing(object sender, System.ComponentModel.CancelEventArgs e)

        {

            // Close instrument, important to release allocated driver data!
            instrument.Dispose();

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }


        private void button1_Click(object sender, EventArgs e)
        {
            
            Console.WriteLine("Set WFS to internal/USER reference plane.\n");
            Console.WriteLine("0 for internal reference.\n");
                        
            int sampleRefPlane = 0;
            
                    instrument.SetReferencePlane(sampleRefPlane);

            int Reference_Index;
            instrument.GetReferencePlane(out Reference_Index);
            textBox2.Text = Reference_Index.ToString();
                                        
        }



        private void Button2_Click(object sender, EventArgs e) // Set External Reference
        {          

         }

        private void button3_Click(object sender, EventArgs e)
        {
        }

        private void button_CreateDefaultUserReference_Click(object sender, EventArgs e)
        {
                        instrument.CreateDefaultUserReference();
                                 
        }

        private void button2_Click_1(object sender, EventArgs e)
        {

            int sampleRefPlane = 1;
            
                    instrument.SaveUserRefFile();

                    instrument.SetReferencePlane(sampleRefPlane);

            int Reference_Index;
            instrument.GetReferencePlane(out Reference_Index);
            textBox2.Text = Reference_Index.ToString();
                                                  
        }

        private void button4_Click(object sender, EventArgs e)
        {


            this.AdjustImageBrightness();
            
            this.GetSpotfieldImage();

            this.CalcSpotCentroids();

            this.CalcBeamCentroid();

            this.CalcSpotDeviations();

            //----

            this.CalcWavefront();

            float[,] wavefront = new float[WFS.MaxSpotY, WFS.MaxSpotX];

            if (0 == instrument.CalcWavefront(sampleWavefrontType, sampleOptionLimitToPupil, wavefront))
            {
                this.textBox_wavefront.Text = wavefront[1, 1].ToString();

                this.textBox_MaxSpotX.Text = WFS.MaxSpotX.ToString();
                this.textBox_MaxSpotY.Text = WFS.MaxSpotY.ToString();

            }

            // save wavefrontdata in txt

            string DocPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            using (StreamWriter outfile = new StreamWriter(Path.Combine(DocPath, "wavefront.txt")))
            {

                for (int x = 0; x < WFS.MaxSpotX; x++)
                {
                    string content = "";

                    for (int y = 0; y < WFS.MaxSpotY; y++)

                    {
                        content += wavefront[x, y].ToString("0.000") + ",";
                        
                    }
                    outfile.WriteLine(content);

                }

            }

            //----

            this.CalcZernikes();
            int zernike_order = sampleZernikeOrders;  //3
            int maxZernikeModes_plusone = maxZernikeModes + 1;

            float[] zernikeOrdersRmsUm = new float[maxZernikeModes_plusone]; // maxZernikeModes = 66
            double rocMm;


            float[] ZernikeUm1 = new float[maxZernikeModes_plusone];
            if (0 == instrument.ZernikeLsf(out zernike_order, ZernikeUm1, zernikeOrdersRmsUm, out rocMm))

            {
                this.textBox_ZernikeUm_1.Text = ZernikeUm1[1].ToString();
                this.textBox_ZernikeUm_2.Text = ZernikeUm1[2].ToString();
                this.textBox_ZernikeUm_3.Text = ZernikeUm1[3].ToString();
                this.textBox_ZernikeUm_4.Text = ZernikeUm1[4].ToString();
                this.textBox_ZernikeUm_5.Text = ZernikeUm1[5].ToString();

            }

            // save Zernike Parameters to text

            using (StreamWriter outfile = new StreamWriter(Path.Combine(DocPath, "Zernike.txt")))
            {

                for (int x = 0; x < maxZernikeModes_plusone; x++)
                {

                    string content = "";

                    content += ZernikeUm1[x].ToString("0.000") + ",";

                    outfile.WriteLine(content);
                }

            }

        }


        private void button5_Click(object sender, EventArgs e)
        {
            instrument.SetSpotsToUserReference();

        }

        private void button3_Click_1(object sender, EventArgs e)
        {


            this.AdjustImageBrightness();
            
            this.GetSpotfieldImage();

            this.CalcSpotCentroids();

            this.CalcBeamCentroid();

            this.CalcSpotDeviations();

            this.CalcWavefront();

            float[,] wavefront = new float[WFS.MaxSpotY, WFS.MaxSpotX];

            if (0 == instrument.CalcWavefront(sampleWavefrontType, sampleOptionLimitToPupil, wavefront))
            {
                this.textBox_wavefront.Text = wavefront[1, 1].ToString();

                this.textBox_MaxSpotX.Text = WFS.MaxSpotX.ToString();
                this.textBox_MaxSpotY.Text = WFS.MaxSpotY.ToString();

            }


            // save wavefrontdata in txt

            string DocPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            using (StreamWriter outfile = new StreamWriter(Path.Combine(DocPath, "wavefront.txt")))
            {

                for (int x = 0; x < WFS.MaxSpotX; x++)
                {
                    string content = "";

                    for (int y = 0; y < WFS.MaxSpotY; y++)

                    {
                        content += wavefront[x, y].ToString("0.000") + ",";
                        
                    }
                    outfile.WriteLine(content);

                }

            }


            //----


            this.CalcZernikes();
            int zernike_order = sampleZernikeOrders;  //3
            int maxZernikeModes_plusone = maxZernikeModes + 1;

            float[] zernikeOrdersRmsUm = new float[maxZernikeModes_plusone]; // maxZernikeModes = 66
            double rocMm;


            float[] ZernikeUm1 = new float[maxZernikeModes_plusone];
            if (0 == instrument.ZernikeLsf(out zernike_order, ZernikeUm1, zernikeOrdersRmsUm, out rocMm))

            {
                this.textBox_ZernikeUm_1.Text = ZernikeUm1[1].ToString();
                this.textBox_ZernikeUm_2.Text = ZernikeUm1[2].ToString();
                this.textBox_ZernikeUm_3.Text = ZernikeUm1[3].ToString();
                this.textBox_ZernikeUm_4.Text = ZernikeUm1[4].ToString();
                this.textBox_ZernikeUm_5.Text = ZernikeUm1[5].ToString();
                
            }

            // save Zernike Parameters to text

            using (StreamWriter outfile = new StreamWriter(Path.Combine(DocPath, "Zernike.txt")))
            {

                for (int x = 0; x < maxZernikeModes_plusone; x++)
                {

                    string content = "";
                    content += ZernikeUm1[x].ToString("0.000") + ",";
                    outfile.WriteLine(content);
                }

            }

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void groupBox5_Enter(object sender, EventArgs e)
        {

        }
    }
    }


