using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace KMTools
{
    public class CreateWindow : GH_Component
    {
        // 1. コンストラクタ
        public CreateWindow()
     : base("Create Window",
        "CrtWin",
        "Creates a single window on a rectangular surface at a specified relative position.\n長方形の壁を対象に、指定した相対位置へ窓を1つ作成します。",
        "KMTools",
        "Windows")
        {
        }

        // 2. 入力
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Surface", "S", "The planar wall surface.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Width", "W", "Desired width of the window (W≠0).", GH_ParamAccess.item, 500.0);
            pManager.AddNumberParameter("Height", "H", "Desired height of the window (H≠0).", GH_ParamAccess.item, 500.0);
            pManager.AddNumberParameter("u Parameter", "u", "Relative horizontal position (0.0 to 1.0).", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("v Parameter", "v", "Relative vertical position (0.0 to 1.0).", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("Margin", "M", "Margin from the wall edge (M≠0).", GH_ParamAccess.item, 200.0);
        }

        // 3. 出力
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Wall with Holes", "W", "The final wall with the window cutout.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Window Outline", "C", "The outline curve of the created window.", GH_ParamAccess.item);
        }

        // 4. メインの処理
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep wallBrep = null;
            double width = 0.0, height = 0.0, u = 0.0, v = 0.0, margin = 0.0;
            const double domain = 1.0; // Domainは1.0で固定

            if (!DA.GetData(0, ref wallBrep)) return;
            if (!DA.GetData(1, ref width)) return;
            if (!DA.GetData(2, ref height)) return;
            if (!DA.GetData(3, ref u)) return;
            if (!DA.GetData(4, ref v)) return;
            if (!DA.GetData(5, ref margin)) return;

            if (wallBrep.Faces.Count != 1 || !wallBrep.Faces[0].IsPlanar(0.001)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Surface (S) must be a single, planar Brep face."); return; }

            if (u < 0 || u > domain || v < 0 || v > domain) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"u/v Parameter is out of the 0 to 1.0 range."); return; }

            BrepFace face = wallBrep.Faces[0];
            Curve outerBoundary = face.OuterLoop.To3dCurve();
            face.TryGetPlane(out Plane wallPlane);

            Curve workAreaBoundary;
            if (margin <= 1e-9)
            {
                workAreaBoundary = outerBoundary;
            }
            else
            {
                Curve[] innerCurves = outerBoundary.Offset(wallPlane, -margin, 0.001, CurveOffsetCornerStyle.Sharp);
                if (innerCurves == null || innerCurves.Length == 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Offsetting the boundary failed. Margin (M) is likely too large."); return; }
                workAreaBoundary = innerCurves[0];
            }

            Brep[] workAreaBreps = Brep.CreatePlanarBreps(workAreaBoundary, 0.001);
            if (workAreaBreps == null || workAreaBreps.Length == 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not create a valid work area from the margin."); return; }
            BrepFace workAreaFace = workAreaBreps[0].Faces[0];

            double u_normalized = u / domain;
            double v_normalized = v / domain;
            double target_u = workAreaFace.Domain(0).Min + u_normalized * workAreaFace.Domain(0).Length;
            double target_v = workAreaFace.Domain(1).Min + v_normalized * workAreaFace.Domain(1).Length;
            Point3d center = workAreaFace.PointAt(target_u, target_v);

            BoundingBox workAreaBBox = workAreaBoundary.GetBoundingBox(wallPlane);
            wallPlane.RemapToPlaneSpace(center, out Point3d centerInPlaneCoords);

            double distRight = Math.Max(0, workAreaBBox.Max.X - centerInPlaneCoords.X);
            double distLeft = Math.Max(0, centerInPlaneCoords.X - workAreaBBox.Min.X);
            double distTop = Math.Max(0, workAreaBBox.Max.Y - centerInPlaneCoords.Y);
            double distBottom = Math.Max(0, centerInPlaneCoords.Y - workAreaBBox.Min.Y);

            double halfWidth = width / 2.0;
            double halfHeight = height / 2.0;

            double finalDistRight = Math.Min(halfWidth, distRight);
            double finalDistLeft = Math.Min(halfWidth, distLeft);
            double finalDistTop = Math.Min(halfHeight, distTop);
            double finalDistBottom = Math.Min(halfHeight, distBottom);

            Plane rectPlane = new Plane(center, wallPlane.XAxis, wallPlane.YAxis);
            Interval widthInterval = new Interval(-finalDistLeft, finalDistRight);
            Interval heightInterval = new Interval(-finalDistBottom, finalDistTop);
            Rectangle3d finalRect = new Rectangle3d(rectPlane, widthInterval, heightInterval);

            List<Curve> curvesForHoles = new List<Curve> { outerBoundary, finalRect.ToNurbsCurve() };
            Brep[] wallWithHolesArray = Brep.CreatePlanarBreps(curvesForHoles, 0.001);

            if (wallWithHolesArray != null && wallWithHolesArray.Length > 0)
            {
                DA.SetData(0, wallWithHolesArray[0]);
                DA.SetData(1, finalRect.ToNurbsCurve()); // ← 追加
            }
            else
            {
                // 穴あけに失敗した場合は、元の壁を返し、輪郭線は空(null)にする
                DA.SetData(0, wallBrep);
                DA.SetData(1, null);
            }
        }

        // 5. アイコンとGUID
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("E149001E-FE6A-4344-AFB0-3F52B75AB100"); 
    }
}

namespace KMTools
{
    public class AnalyzeWindowView : GH_Component
    {
        // 1. コンストラクタ
        public AnalyzeWindowView()
     : base("AnalyzeWindowView_v1.0", "WndView",
       "Calculates the area of intersection between a frame and a list of surfaces, and multiplies it by a corresponding factor.",
       "KMTools", "Windows")
        {
        }

        // 2. 入力
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Frame Curve", "C", "The closed planar curve of the window frame.", GH_ParamAccess.item);
            pManager.AddBrepParameter("Surfaces", "S", "A list of surfaces to intersect with the frame.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Factors", "F", "A list of factors corresponding to each surface.", GH_ParamAccess.list);
        }

        // 3. 出力
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Factored Areas", "A", "A list of calculated areas multiplied by the factors.", GH_ParamAccess.list);
            pManager.AddBrepParameter("Intersection Geometry", "G", "The resulting Brep geometry from the intersection.", GH_ParamAccess.list);
        }

        // 4. メインの処理
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // --- 入力データを取得 ---
            Curve frameCurve = null;
            List<Brep> surfaces = new List<Brep>();
            List<double> factors = new List<double>();

            if (!DA.GetData(0, ref frameCurve)) return;
            if (!DA.GetDataList(1, surfaces)) return;
            if (!DA.GetDataList(2, factors)) return;

            // --- 初期チェック ---
            //リストの数が違う場合はエラー
            if (surfaces.Count != factors.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The number of Surfaces must be equal to the number of Factors.");
                return;
            }
            if (!frameCurve.IsClosed || !frameCurve.IsPlanar())
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Frame Curve (C) must be a closed, planar curve.");
                return;
            }
            frameCurve.TryGetPlane(out Plane framePlane);

            // --- 準備 ---
            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            List<double> factoredAreas = new List<double>();
            List<Brep> intersectionGeometries = new List<Brep>();

            for (int i = 0; i < surfaces.Count; i++)
            {
                Brep currentSurface = surfaces[i];
                double currentFactor = factors[i];

                double totalIntersectionArea = 0.0;
                List<Brep> currentIntersectionParts = new List<Brep>();

                if (currentSurface != null)
                {
                    Brep[] splitPieces = currentSurface.Split(new Curve[] { frameCurve }, tolerance);
                    if (splitPieces != null && splitPieces.Length > 0)
                    {
                        foreach (Brep piece in splitPieces)
                        {
                            if (piece.GetArea() < 1e-9) continue;

                            bool isInsidePiece = true; // まずは内側と仮定
                            foreach (BrepVertex vertex in piece.Vertices)
                            {
                                // 各頂点が、窓枠の外側か判定
                                if (frameCurve.Contains(vertex.Location, framePlane, tolerance) == PointContainment.Outside)
                                {
                                    // 一つでも外側の頂点が見つかれば、この破片は「外側」と確定
                                    isInsidePiece = false;
                                    break;
                                }
                            }

                            // 全ての頂点が「外側」ではなかった場合のみ、採用
                            if (isInsidePiece)
                            {
                                totalIntersectionArea += piece.GetArea();
                                currentIntersectionParts.Add(piece);
                            }
                        }
                    }
                }

                factoredAreas.Add(totalIntersectionArea * currentFactor);
                intersectionGeometries.AddRange(currentIntersectionParts);
            }

            // --- 出力 ---
            DA.SetDataList(0, factoredAreas);
            DA.SetDataList(1, intersectionGeometries);
        }

        // 5. アイコンとGUID
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("B658BC4E-8194-403C-8CC2-053D42A83D7B");
    }
}

namespace KMTools
{
    public class CreateLouverComponent : GH_Component
    {
        // 1. コンストラクタ
        public CreateLouverComponent()
     : base("Create Louvers v1.1", "Louvers",
       "Creates a series of louvers along a curve.",
       "KMTools", "Louvers")
        {
        }

        // 2. 入力
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curve", "C", "The path curve for the louvers.", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Number", "N", "The number of louvers.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("Angle", "A", "The rotation angle of the louvers (in degrees).", GH_ParamAccess.item, 0.0);
            pManager.AddNumberParameter("Length", "L", "The length of each louver (perpendicular to the curve).", GH_ParamAccess.item, 200.0);
            pManager.AddNumberParameter("Width", "W", "The width of each louver (parallel to the curve).", GH_ParamAccess.item, 100.0);
            pManager.AddNumberParameter("Height", "H", "The height (thickness) of each louver.", GH_ParamAccess.item, 1000.0);
            pManager.AddBooleanParameter("Remove Ends", "R", "If true, the first and last louvers will be removed.", GH_ParamAccess.item, false);
        }

        // 3. 出力
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Louvers", "B", "The resulting louver geometry.", GH_ParamAccess.list);
        }

        // 4. メインの処理
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve curve = null;
            int number = 0;
            double angleDegrees = 0.0;
            double lengthX = 0.0;
            double lengthY = 0.0;
            double height = 0.0;
            bool removeEnds = false;

            if (!DA.GetData(0, ref curve)) return;
            if (!DA.GetData(1, ref number)) return;
            if (!DA.GetData(2, ref angleDegrees)) return;
            if (!DA.GetData(3, ref lengthX)) return;
            if (!DA.GetData(4, ref lengthY)) return;
            if (!DA.GetData(5, ref height)) return;
            if (!DA.GetData(6, ref removeEnds)) return;

            int minNumber = removeEnds ? 3 : 1;
            if (number < minNumber) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Number (N) must be at least {minNumber}."); return; }
            if (curve == null || !curve.IsValid) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input curve is not valid."); return; }
            if (lengthX <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Length (L) must be a positive number."); return; }
            if (lengthY <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Width (W) must be a positive number."); return; }
            if (Math.Abs(height) < 1e-9) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Height (H) cannot be zero."); return; }

            List<Brep> louvers = new List<Brep>();
            double angleRad = Rhino.RhinoMath.ToRadians(angleDegrees);

            double rotatedHalfWidth = (lengthY / 2.0) * Math.Abs(Math.Cos(angleRad)) + (lengthX / 2.0) * Math.Abs(Math.Sin(angleRad));
            double curveLength = curve.GetLength();
            double spanForCenters = curveLength - (2 * rotatedHalfWidth);
            if (spanForCenters < -1e-9) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Louvers are too large to fit within the curve's span."); return; }

            int startIndex = removeEnds ? 1 : 0;
            int endIndex = removeEnds ? number - 1 : number;

            for (int i = startIndex; i < endIndex; i++)
            {
                double lengthOnCurve = rotatedHalfWidth + spanForCenters * i / (number - 1);
                if (!curve.LengthParameter(lengthOnCurve, out double t)) continue;

                Point3d center = curve.PointAt(t);
                Vector3d tangent = curve.TangentAt(t);

                Vector3d localY = Vector3d.CrossProduct(Vector3d.ZAxis, tangent);
                Plane localPlane = new Plane(center, tangent, localY);

                Interval zInterval = (height > 0) ? new Interval(0, height) : new Interval(height, 0);

                Box box = new Box(localPlane,
                  new Interval(-lengthY / 2.0, lengthY / 2.0),
                  new Interval(-lengthX / 2.0, lengthX / 2.0),
                  zInterval);

                box.Transform(Transform.Rotation(angleRad, Vector3d.ZAxis, center));
                louvers.Add(box.ToBrep());
            }

            DA.SetDataList(0, louvers);
        }

        // 5. アイコンとGUID
        protected override System.Drawing.Bitmap Icon => null;
        public override Guid ComponentGuid => new Guid("7E16A838-03CA-4E55-9558-59CF0C73643B");
    }
}