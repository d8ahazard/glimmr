using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using HueDream.Models.Util;
using Newtonsoft.Json;

namespace HueDream.Models.DreamGrab {
    public class Calibrate {
        public static void ProcessFrames() {
            var cornersObjectList = new List<MCvPoint3D32f[]>();
            var cornersPointsList = new List<PointF[]>();
            var cornersPointsVec = new List<VectorOfPointF>();
            var width = 8; //width of chessboard no. squares in width - 1
            var height = 6; // heght of chess board no. squares in heigth - 1
            float squareSize = width * height;
            Size patternSize = new Size(width, height); //size of chess board to be detected
            VectorOfPointF corners = new VectorOfPointF(); //corners found from chessboard

            Mat[] _rvecs, _tvecs;

            List<Mat> frameArrayBuffer = new List<Mat>();
            var startFlag = true;

            var cameraMatrix = new Mat(3, 3, DepthType.Cv64F, 1);
            var distCoeffs = new Mat(8, 1, DepthType.Cv64F, 1);

            var frames = new List<Mat>();
            // Glob our frames from the static dir, loop for them
            string[] filePaths = Directory.GetFiles(@"/home/dietpi/", "*.jpg");
            foreach (var path in filePaths) {
                frames.Add(CvInvoke.Imread(path));
            }
            LogUtil.Write("We have " + frames.Count + " frames.");
            var fc = 0;
            foreach (var frame in frames) {
                var grayFrame = new Mat();

                // Convert to grayscale
                CvInvoke.CvtColor(frame, grayFrame, ColorConversion.Bgr2Gray);

                //apply chess board detection
                var boardFound = CvInvoke.FindChessboardCorners(grayFrame, patternSize, corners,
                    CalibCbType.AdaptiveThresh | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
                //we use this loop so we can show a colour image rather than a gray:
                if (boardFound) {
                    LogUtil.Write("Found board in frame " + fc);
                    //make measurements more accurate by using FindCornerSubPixel
                    CvInvoke.CornerSubPix(grayFrame, corners, new Size(11, 11), new Size(-1, -1),
                        new MCvTermCriteria(30, 0.1));
                    frameArrayBuffer.Add(grayFrame);
                }

                fc++;
                corners = new VectorOfPointF();
            }
            LogUtil.Write("We have " + frameArrayBuffer.Count + " frames to use for mapping.");
            // Loop through frames where board was detected
            foreach (var frame in frameArrayBuffer) {
                var frameVect = new VectorOfPointF();
                CvInvoke.FindChessboardCorners(frame, patternSize, frameVect,
                    CalibCbType.AdaptiveThresh
                    | CalibCbType.FastCheck | CalibCbType.NormalizeImage);
                //for accuracy
                CvInvoke.CornerSubPix(frame, frameVect, new Size(11, 11), new Size(-1, -1),
                    new MCvTermCriteria(30, 0.1));

                //Fill our objects list with the real world measurements for the intrinsic calculations
                var objectList = new List<MCvPoint3D32f>();
                for (int i = 0; i < height; i++) {
                    for (int j = 0; j < width; j++) {
                        objectList.Add(new MCvPoint3D32f(j * squareSize, i * squareSize, 0.0F));
                    }
                }

                //corners_object_list[k] = new MCvPoint3D32f[];
                cornersObjectList.Add(objectList.ToArray());
                cornersPointsList.Add(frameVect.ToArray());
                frameVect.Dispose();
            }

            //our error should be as close to 0 as possible

            double error = CvInvoke.CalibrateCamera(cornersObjectList.ToArray(), cornersPointsList.ToArray(), frames[0].Size,
                cameraMatrix, distCoeffs, CalibType.RationalModel, new MCvTermCriteria(30, 0.1), out _rvecs,
                out _tvecs);
            LogUtil.Write("Correction error: " + error);
            var sk = JsonConvert.SerializeObject(cameraMatrix);
            var sd = JsonConvert.SerializeObject(distCoeffs);

            LogUtil.Write("Cameramatrix: " + sk);
            LogUtil.Write("Dist coeff: " + sd);
            var s = JsonConvert.DeserializeObject(sk);
            var d = JsonConvert.DeserializeObject(sd);
            DreamData.SetItem("k", sk);
            DreamData.SetItem("d", sd);
        }
    }
}