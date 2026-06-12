using SAM.Core;
using SAM.Core.UI;
using SAM.Core.UI.WPF;
using SAM.Geometry.Object;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;

namespace SAM.Geometry.UI.WPF
{
    /// <summary>
    /// Interaction logic for ViewportControl.xaml
    /// </summary>
    public partial class ViewportControl : UserControl
    {
        // Experimental 2D floor plan renderer (see FloorPlan2DControl) - opt-in, off by default
        private static readonly bool floorPlan2DEnabled = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SAM_UI_FLOORPLAN_2D")) && Environment.GetEnvironmentVariable("SAM_UI_FLOORPLAN_2D").Trim() != "0";

        private ActionManager actionManager;
        private Mode mode = Mode.ThreeDimensional;

        private RectangularSelector rectangularSelector;
        private UIGeometryObjectModel uIGeometryObjectModel;

        private FloorPlan2DControl floorPlan2DControl;

        public ViewportControl()
        {
            InitializeComponent();

            helixViewport3D.PanGesture = new MouseGesture(MouseAction.LeftClick, ModifierKeys.Shift);
            helixViewport3D.RotateGesture = new MouseGesture(MouseAction.RightClick, ModifierKeys.Shift);
            uIGeometryObjectModel = new UIGeometryObjectModel();

            helixViewport3D.Loaded += helixViewport3D_Loaded;

            actionManager = new ActionManager();
            rectangularSelector = new RectangularSelector(grid);
            rectangularSelector.Selected += RectangularSelector_Selected;

            rectangularSelector.Selecting += RectangularSelector_Selecting;

            floorPlan2DControl = new FloorPlan2DControl() { Visibility = Visibility.Collapsed, ContextMenu = new ContextMenu() };
            grid.Children.Insert(grid.Children.IndexOf(helixViewport3D) + 1, floorPlan2DControl);
            floorPlan2DControl.ObjectHoovered += FloorPlan2DControl_ObjectHoovered;
            floorPlan2DControl.ObjectDoubleClicked += FloorPlan2DControl_ObjectDoubleClicked;
            floorPlan2DControl.ObjectSelectionChanged += FloorPlan2DControl_ObjectSelectionChanged;
            floorPlan2DControl.ContextMenuOpening += FloorPlan2DControl_ContextMenuOpening;
        }

        public event ObjectContextMenuOpeningEventHandler ObjectContextMenuOpening;

        public event ObjectDoubleClickedEventHandler ObjectDoubleClicked;

        public event ObjectHooveredEventHandler ObjectHoovered;
        public event ObjectSelectionChangedEventHandler ObjectSelectionChanged;

        private bool Active2D
        {
            get
            {
                return floorPlan2DEnabled && mode == Mode.TwoDimensional;
            }
        }

        private void FloorPlan2DControl_ObjectHoovered(object sender, ObjectHooveredEventArgs e)
        {
            ObjectHoovered?.Invoke(this, e);
        }

        private void FloorPlan2DControl_ObjectDoubleClicked(object sender, ObjectDoubleClickedEventArgs e)
        {
            ObjectDoubleClicked?.Invoke(this, e);
        }

        private void FloorPlan2DControl_ObjectSelectionChanged(object sender, ObjectSelectionChangedEventArgs e)
        {
            ObjectSelectionChanged?.Invoke(this, e);
        }

        private void FloorPlan2DControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl) ||
                Keyboard.IsKeyDown(Key.LeftAlt) ||
                Keyboard.IsKeyDown(Key.RightAlt) ||
                Keyboard.IsKeyDown(Key.LeftShift) ||
                Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                return;
            }

            floorPlan2DControl.ContextMenu = new ContextMenu();

            MenuItem menuItem = new MenuItem();
            menuItem.Name = "MenuItem_ZoomExtents";
            menuItem.Header = "Zoom Extents";
            menuItem.Click += MenuItem_ZoomExtents_Click;
            floorPlan2DControl.ContextMenu.Items.Add(menuItem);

            List<ModelVisual3D> modelVisual3Ds = new List<ModelVisual3D>();
            foreach (SAMObject sAMObject in floorPlan2DControl.SelectedSAMObjects())
            {
                ModelVisual3D modelVisual3D = floorPlan2DControl.GetStubVisual3D(sAMObject.Guid);
                if (modelVisual3D != null)
                {
                    modelVisual3Ds.Add(modelVisual3D);
                }
            }

            ObjectContextMenuOpening?.Invoke(this, new ObjectContextMenuOpeningEventArgs(floorPlan2DControl.ContextMenu, e, modelVisual3Ds));
        }

        public Camera Camera
        {
            get
            {
                return GetCamera();
            }

            set
            {
                SetCamera(value);
            }
        }

        public Guid Guid { get; set; }
        
        public Mode Mode
        {
            get
            {
                return mode;
            }

            set
            {
                if (mode != value)
                {
                    mode = value;
                    UpdateMode();
                }
            }
        }

        public UIGeometryObjectModel UIGeometryObjectModel
        {
            get
            {
                return uIGeometryObjectModel;
            }

            set
            {
                uIGeometryObjectModel = value;
                if (uIGeometryObjectModel != null)
                {
                    uIGeometryObjectModel.Modified -= UIGeometryObjectModel_Modified;
                    uIGeometryObjectModel.Modified += UIGeometryObjectModel_Modified;

                    uIGeometryObjectModel.Closed -= UIGeometryObjectModel_Closed;
                    uIGeometryObjectModel.Closed += UIGeometryObjectModel_Closed;

                    uIGeometryObjectModel.Opened -= UIGeometryObjectModel_Opened;
                    uIGeometryObjectModel.Opened += UIGeometryObjectModel_Opened;
                }

                Load(uIGeometryObjectModel?.JSAMObject);

                gridLinesVisual3D.Visible = uIGeometryObjectModel?.JSAMObject == null;
            }
        }

        public LegendItem UndefinedLegendItem
        {
            get
            {
                return legendControl.UndefinedLegendItem;
            }

            set
            {
                legendControl.UndefinedLegendItem = value;
            }
        }

        public bool ContainsAny<T>(IEnumerable<Guid> guids) where T : SAMObject
        {
            if (Active2D)
            {
                if (guids == null)
                {
                    return false;
                }

                List<T> sAMObjects = floorPlan2DControl.SAMObjects<T>();
                return sAMObjects != null && sAMObjects.Find(x => guids.Contains(x.Guid)) != null;
            }

            return Query.ContainsAny<T>(helixViewport3D.Children, guids);
        }

        public bool Contains<T>(Guid guid) where T : SAMObject
        {
            if (Active2D)
            {
                return ContainsAny<T>([guid]);
            }

            return Query.ContainsAny<T>(helixViewport3D.Children, [guid]);
        }

        public T GetSAMObject<T>(Guid guid) where T : SAMObject
        {
            return Core.UI.WPF.Query.JSAMObject<T>(GetVisual3D<T>(guid));
        }

        public Visual3D GetVisual3D<T>(Guid guid) where T : SAMObject
        {
            if (Active2D)
            {
                ModelVisual3D modelVisual3D = floorPlan2DControl.GetStubVisual3D(guid);
                if (modelVisual3D == null || Core.UI.WPF.Query.JSAMObject<T>(modelVisual3D) == null)
                {
                    return null;
                }

                return modelVisual3D;
            }

            return Core.UI.WPF.Query.Visual3D<T>(helixViewport3D.Children, guid);
        }

        public List<T> SAMObjects<T>() where T : SAMObject
        {
            if (Active2D)
            {
                return floorPlan2DControl.SAMObjects<T>();
            }

            List<ModelVisual3D> visual3Ds = Core.UI.WPF.Query.Visual3Ds<ModelVisual3D>(helixViewport3D.Children, new Type[] { typeof(GeometryObjectModel) });
            if (visual3Ds is null)
            {
                return null;
            }

            List<T> result = [];

            foreach (ModelVisual3D visual3D in visual3Ds)
            {
                T t = Core.UI.WPF.Query.JSAMObject<T>(visual3D);
                if (t is null)
                {
                    continue;
                }

                result.Add(t);
            }

            return result;
        }

        public bool Select(SAMObject sAMObject)
        {
            if (Active2D)
            {
                bool result2D = sAMObject == null ? floorPlan2DControl.ClearSelection() : floorPlan2DControl.Select([sAMObject.Guid]);
                if (result2D)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }

                return result2D;
            }

            bool result = false;

            if (sAMObject == null)
            {
                result = actionManager.Cancel<SelectAction>();
                if (result)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }
                return result;
            }

            Visual3D visual3D = GetVisual3D<SAMObject>(sAMObject.Guid);
            if (visual3D == null)
            {
                return result;
            }

            result = actionManager.Apply(new SelectAction(visual3D));
            if (result)
            {
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
            }

            return result;
        }

        public bool Select<T>(IEnumerable<T> sAMObjects) where T : SAMObject
        {
            if (Active2D)
            {
                bool result2D;
                if (sAMObjects == null)
                {
                    result2D = floorPlan2DControl.ClearSelection();
                }
                else
                {
                    result2D = floorPlan2DControl.Select(new List<T>(sAMObjects).FindAll(x => x != null).ConvertAll(x => x.Guid));
                }

                if (result2D)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }

                return result2D;
            }

            bool result = false;

            if (sAMObjects == null)
            {
                result = actionManager.Cancel<SelectAction>();
                if (result)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }
                return result;
            }

            List<Visual3D> visual3Ds = new List<Visual3D>();
            foreach (T t in sAMObjects)
            {
                Visual3D visual3D = GetVisual3D<SAMObject>(t.Guid);
                if (visual3D == null)
                {
                    continue;
                }

                visual3Ds.Add(visual3D);
            }

            if (visual3Ds == null || visual3Ds.Count == 0)
            {
                result = actionManager.Cancel<SelectAction>();
                if (result)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }

                return result;
            }

            result = actionManager.Apply(new SelectAction(visual3Ds));
            if (result)
            {
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
            }

            return result;
        }
        
        public List<T> SelectedSAMObjects<T>() where T : SAMObject
        {
            if (Active2D)
            {
                List<T> result2D = new List<T>();
                foreach (SAMObject sAMObject in floorPlan2DControl.SelectedSAMObjects())
                {
                    if (sAMObject is T)
                    {
                        result2D.Add((T)sAMObject);
                    }
                }

                return result2D;
            }

            if (actionManager == null)
            {
                return null;
            }

            List<SelectAction> selectActions = actionManager.GetActions<SelectAction>();
            if (selectActions == null || selectActions.Count == 0)
            {
                return null;
            }

            List<T> result = new List<T>();
            foreach (SelectAction selectAction in selectActions)
            {
                List<Visual3D> visual3Ds = selectAction.Visual3Ds;
                if (visual3Ds != null)
                {
                    foreach (Visual3D visual3D in visual3Ds)
                    {
                        ITaggable taggable = visual3D?.JSAMObject<IJSAMObject>() as ITaggable;
                        if (taggable != null)
                        {
                            T t = taggable.Tag?.Value as T;
                            if (t != null)
                            {
                                result.Add(t);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public bool Zoom(SAMObject sAMObject)
        {
            if (sAMObject == null)
            {
                return false;
            }

            if (Active2D)
            {
                if (!floorPlan2DControl.Contains(sAMObject.Guid))
                {
                    return false;
                }

                floorPlan2DControl.Zoom([sAMObject.Guid]);
                return true;
            }

            Visual3D visual3D = GetVisual3D<SAMObject>(sAMObject.Guid);
            if (visual3D == null)
            {
                return false;
            }

            Rect3D rect3D = visual3D.Bounds();
            if (rect3D == Rect3D.Empty)
            {
                return false;
            }

            helixViewport3D.ZoomExtents(rect3D);
            return true;
        }

        public bool Zoom<T>(IEnumerable<T> sAMObjects) where T : SAMObject
        {
            if (sAMObjects == null)
            {
                return false;
            }

            if (Active2D)
            {
                List<Guid> guids = new List<T>(sAMObjects).FindAll(x => x != null && floorPlan2DControl.Contains(x.Guid)).ConvertAll(x => x.Guid);
                if (guids.Count == 0)
                {
                    return false;
                }

                floorPlan2DControl.Zoom(guids);
                return true;
            }

            List<Rect3D> rect3Ds = new List<Rect3D>();
            foreach (SAMObject sAMObject in sAMObjects)
            {
                Visual3D visual3D = GetVisual3D<SAMObject>(sAMObject.Guid);
                if (visual3D == null)
                {
                    continue;
                }

                Rect3D rect3D = visual3D.Bounds();
                if (rect3D == Rect3D.Empty)
                {
                    continue;
                }

                rect3Ds.Add(rect3D);
            }


            Rect3D rect3D_Union = Rect3D.Empty;
            foreach (Rect3D rect3D_Temp in rect3Ds)
            {
                rect3D_Union.Union(rect3D_Temp);
            }

            if (rect3D_Union == Rect3D.Empty)
            {
                return false;
            }

            helixViewport3D.ZoomExtents(rect3D_Union);
            return true;
        }

        private Camera GetCamera()
        {
            ProjectionCamera projectionCamera = helixViewport3D.Camera;
            if (projectionCamera == null)
            {
                return null;
            }

            return new Camera(projectionCamera.Position.ToSAM(), projectionCamera.LookDirection.ToSAM(), projectionCamera.UpDirection.ToSAM());
        }

        private void helixViewport3D_CameraChanged(object sender, RoutedEventArgs e)
        {
        }

        private void helixViewport3D_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) ||
                Keyboard.IsKeyDown(Key.RightCtrl) ||
                Keyboard.IsKeyDown(Key.LeftAlt) ||
                Keyboard.IsKeyDown(Key.RightAlt) ||
                Keyboard.IsKeyDown(Key.LeftShift) ||
                Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                return;
            }

            helixViewport3D.ContextMenu = new ContextMenu();

            MenuItem menuItem = new MenuItem();
            menuItem.Name = "MenuItem_ZoomExtents";
            menuItem.Header = "Zoom Extents";
            menuItem.Click += MenuItem_ZoomExtents_Click;
            helixViewport3D.ContextMenu.Items.Add(menuItem);

            ObjectContextMenuOpening?.Invoke(this, new ObjectContextMenuOpeningEventArgs(helixViewport3D.ContextMenu, e, actionManager.SelectedVisual3Ds()?.FindAll(x => x is ModelVisual3D)?.ConvertAll(x => x as ModelVisual3D)));
        }

        private void helixViewport3D_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                bool cancelled = actionManager.Cancel<SelectAction>();
                if (cancelled)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }
            }
        }

        private void helixViewport3D_Loaded(object sender, RoutedEventArgs e)
        {
            GeometryObjectModel geometryObjectModel = uIGeometryObjectModel?.JSAMObject;

            if (geometryObjectModel != null && geometryObjectModel.TryGetValue(GeometryObjectModelParameter.ViewSettings, out ViewSettings viewSettings) && viewSettings != null)
            {
                Camera camera = viewSettings.Camera;
                if (camera != null)
                {
                    helixViewport3D.Camera.Position = camera.Location.ToMedia3D();
                    helixViewport3D.Camera.LookDirection = camera.LookDirection.ToMedia3D();

                    if(camera.UpDirection is not null)
                    {
                        helixViewport3D.Camera.UpDirection = camera.UpDirection.ToMedia3D(); //NEW 2026.03.16
                    }
                }
                else
                {
                    helixViewport3D.ZoomExtents();
                }
            }

            helixViewport3D.Loaded -= helixViewport3D_Loaded;
        }

        private void helixViewport3D_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point point_Current = e.GetPosition(helixViewport3D);

                RayMeshGeometry3DHitTestResult rayMeshGeometry3DHitTestResult = Core.UI.WPF.Query.RayMeshGeometry3DHitTestResult(helixViewport3D, point_Current, out ModelVisual3D modelVisual3D);
                if (rayMeshGeometry3DHitTestResult != null && modelVisual3D != null)
                {
                    ObjectDoubleClicked?.Invoke(this, new ObjectDoubleClickedEventArgs(e, modelVisual3D));
                }
            }
        }

        private void helixViewport3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point point_Current = e.GetPosition(helixViewport3D);

            RayMeshGeometry3DHitTestResult rayMeshGeometry3DHitTestResult = Core.UI.WPF.Query.RayMeshGeometry3DHitTestResult(helixViewport3D, point_Current, out ModelVisual3D modelVisual3D);

            bool cancelled = false;
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
            {
                cancelled = actionManager.Cancel<SelectAction>();
            }

            if (rayMeshGeometry3DHitTestResult == null && modelVisual3D == null)
            {
                e.Handled = true;

                if (cancelled)
                {
                    ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                }

                return;
            }

            SelectAction selectAction = actionManager.GetAction<SelectAction>();
            if (selectAction == null)
            {
                selectAction = new SelectAction();
            }

            if (selectAction != null && selectAction.Contains(modelVisual3D))
            {
                selectAction.Remove(modelVisual3D);
            }
            else
            {
                selectAction.Add(modelVisual3D);
            }

            actionManager.Apply(selectAction);
            ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());

        }

        private void helixViewport3D_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            actionManager.Cancel<HighlightAction>();

            Point point = e.GetPosition(helixViewport3D);

            RayMeshGeometry3DHitTestResult rayMeshGeometry3DHitTestResult;
            ModelVisual3D modelVisual3D;

            // Only slow hit-tests (>= 25 ms) are logged to avoid flooding the log on mouse move
            using (PerformanceLog.Measure("ViewportControl.HoverHitTest", mode.ToString(), 25))
            {
                rayMeshGeometry3DHitTestResult = Core.UI.WPF.Query.RayMeshGeometry3DHitTestResult(helixViewport3D, point, out modelVisual3D);
            }
            if (rayMeshGeometry3DHitTestResult != null && modelVisual3D != null)
            {
                actionManager.Apply(new HighlightAction(modelVisual3D));

                ObjectHoovered?.Invoke(this, new ObjectHooveredEventArgs(e, modelVisual3D));
            }
        }

        private void Load(GeometryObjectModel geometryObjectModel)
        {
            helixViewport3D.Lights.Children.Clear();
            helixViewport3D.Lights.Children.Add(new AmbientLight());

            bool cancelled = actionManager.Cancel();
            if (cancelled)
            {
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
            }

            int count = Core.UI.WPF.Query.Visual3Ds<ModelVisual3D>(helixViewport3D.Children, new Type[] { typeof(GeometryObjectModel) }).Count;
            if (count > 0)
            {
                Core.UI.WPF.Modify.Clear<ModelVisual3D>(helixViewport3D.Children, new Type[] { typeof(GeometryObjectModel) });
            }

            // Flag-gated 2D floor plan path: render via FloorPlan2DControl, keep the Helix scene empty
            floorPlan2DControl.Load(Active2D ? geometryObjectModel : null);

            if (geometryObjectModel == null)
            {
                return;
            }

            if (!Active2D)
            {
                ModelVisual3D modelVisual3D;
                using (PerformanceLog.Measure("ViewportControl.ToMedia3D", mode.ToString()))
                {
                    modelVisual3D = Convert.ToMedia3D(geometryObjectModel);
                }

                if (modelVisual3D != null)
                {
                    helixViewport3D.Children.Add(modelVisual3D);
                }

                if (count == 0)
                {
                    helixViewport3D.ZoomExtents();
                }
            }

            legendControl.Visibility = Visibility.Hidden;
            if (geometryObjectModel.TryGetValue(GeometryObjectModelParameter.ViewSettings, out ViewSettings viewSettings))
            {
                Legend legend = viewSettings.Legend;
                List<LegendItem> legendItems = legend?.LegendItems;

                if (legend != null && legend.Visible && legendItems != null && legendItems.Count != 0)
                {
                    legendControl.Visibility = Visibility.Visible;
                    legendControl.Legend = legend;
                }
            }
        }

        private void MenuItem_ZoomExtents_Click(object sender, RoutedEventArgs e)
        {
            if (Active2D)
            {
                floorPlan2DControl.ZoomExtents();
                return;
            }

            helixViewport3D.ZoomExtents();
        }

        private void RectangularSelector_Selected(object sender, EventArgs e)
        {
            actionManager.Cancel<HighlightAction>();

            if (rectangularSelector == null)
            {
                return;
            }

            Rect rect = rectangularSelector.Rect;
            if (rect == null || rect == Rect.Empty)
            {
                return;
            }

            SelectionType selectionType = rectangularSelector.SelectionType;
            if (selectionType == SelectionType.Undefined)
            {
                return;
            }

            Select(rect, selectionType);

            //List<Spatial.Point3D> point3Ds = new List<Spatial.Point3D>();

            //foreach (Point2D point2D in point2Ds)
            //{
            //    Point point = new Point(point2D.X, point2D.Y);

            //    //point = helixViewport3D.Viewport.PointFromScreen(point);

            //    HelixToolkit.Wpf.Viewport3DHelper.Point2DtoPoint3D(helixViewport3D.Viewport, point, out Point3D point3D_Near, out Point3D point3D_Far);

            //    Spatial.Point3D point3D_Near_SAM = point3D_Near.ToSAM();
            //    point3D_Near_SAM = new Spatial.Point3D(point3D_Near_SAM.X, point3D_Near.Y, 5);

            //    Spatial.Point3D point3D_Far_SAM = point3D_Far.ToSAM();
            //    point3D_Far_SAM = new Spatial.Point3D(point3D_Far_SAM.X, point3D_Far.Y, -10);

            //    point3Ds.Add(point3D_Near_SAM);
            //    point3Ds.Add(point3D_Far_SAM);
            //}

            //Spatial.BoundingBox3D boundingBox3D = new Spatial.BoundingBox3D(point3Ds);

            //List<Spatial.ISAMGeometry3DObject> sAMGeometry3DObjects = null;
            //switch (selectionType)
            //{
            //    case SelectionType.Inside:
            //        sAMGeometry3DObjects = uIGeometryObjectModel?.JSAMObject?.GetSAMGeometryObjects<Spatial.ISAMGeometry3DObject>(x => x is Spatial.ISAMGeometry3DObject && Spatial.Query.Inside(boundingBox3D, x, true));
            //        break;

            //    case SelectionType.InsideOrIntersect:
            //        sAMGeometry3DObjects = uIGeometryObjectModel?.JSAMObject?.GetSAMGeometryObjects<Spatial.ISAMGeometry3DObject>(x => x is Spatial.ISAMGeometry3DObject && Spatial.Query.InRange(boundingBox3D, x));
            //        break;
            //}

            //if (sAMGeometry3DObjects == null)
            //{
            //    return;
            //}

            //Select(sAMGeometry3DObjects.ConvertAll(x => x as ITaggable).FindAll(x => x != null).ConvertAll(x => x?.Tag?.Value as SAMObject));

        }

        private void RectangularSelector_Selecting(object sender, EventArgs e)
        {

        }

        private void Select(Rect rect, SelectionType selectionType)
        {
            actionManager.Cancel<HighlightAction>();

            if (rect == null || rect == Rect.Empty)
            {
                return;
            }

            if (selectionType == SelectionType.Undefined)
            {
                return;
            }

            if (Active2D)
            {
                floorPlan2DControl.SelectByScreenRect(rect, selectionType);
                ObjectSelectionChanged?.Invoke(this, new ObjectSelectionChangedEventArgs());
                return;
            }

            List<SAMObject> sAMObjects = new List<SAMObject>();
            IEnumerable<HelixToolkit.Wpf.Viewport3DHelper.RectangleHitResult> rectangleHitResults = HelixToolkit.Wpf.Viewport3DHelper.FindHits(helixViewport3D.Viewport, rect, selectionType == SelectionType.Inside ? HelixToolkit.Wpf.SelectionHitMode.Inside : HelixToolkit.Wpf.SelectionHitMode.Touch);
            if (rectangleHitResults != null)
            {
                foreach (HelixToolkit.Wpf.Viewport3DHelper.RectangleHitResult rectangleHitResult in rectangleHitResults)
                {
                    IJSAMObject jSAMObject_Model = Core.UI.WPF.Query.JSAMObject<IJSAMObject>(rectangleHitResult.Model);
                    IJSAMObject jSAMObject_Visual = Core.UI.WPF.Query.JSAMObject<IJSAMObject>(rectangleHitResult.Visual);

                    if (jSAMObject_Model is Text3DObject)
                    {
                        continue;
                    }

                    SAMObject sAMObject = (jSAMObject_Visual as ITaggable)?.Tag?.Value as SAMObject;
                    if (sAMObject != null)
                    {
                        Visual3D visual3D = GetVisual3D<SAMObject>(sAMObject.Guid);
                        if (jSAMObject_Model is ISAMGeometry3DObject && jSAMObject_Visual is IEnumerable<ISAMGeometry3DObject>)
                        {
                            ISAMGeometry3DObject sAMGeometry3DObject = (ISAMGeometry3DObject)jSAMObject_Model;
                            IEnumerable<ISAMGeometry3DObject> sAMGeometry3DObjects = (IEnumerable<ISAMGeometry3DObject>)jSAMObject_Visual;
                            if (!sAMGeometry3DObjects.Contains(sAMGeometry3DObject))
                            {
                                continue;
                            }
                        }

                        sAMObjects.Add(sAMObject);
                    }

                }
            }

            Select(sAMObjects);
        }
        
        private void SetCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            ProjectionCamera projectionCamera = helixViewport3D.Camera;
            if (projectionCamera == null)
            {
                return;
            }

            Spatial.Vector3D lookDirection = camera.LookDirection;
            if (lookDirection.AlmostEqual(-Spatial.Vector3D.WorldZ, Tolerance.MacroDistance))
            {
                lookDirection = new Spatial.Vector3D(0, 0.0001, -0.9999);
            }
            else if (lookDirection.AlmostEqual(Spatial.Vector3D.WorldZ, Tolerance.MacroDistance))
            {
                lookDirection = new Spatial.Vector3D(0, 0.0001, 0.9999);
            }

            projectionCamera.LookDirection = lookDirection.ToMedia3D();
            projectionCamera.Position = camera.Location.ToMedia3D();

            if(camera.UpDirection is not null)
            {
                projectionCamera.UpDirection = camera.UpDirection.ToMedia3D(); //NEW 2026.03.16
            }
        }

        private void UIGeometryObjectModel_Closed(object sender, EventArgs e)
        {
            Load(uIGeometryObjectModel?.JSAMObject);
        }

        private void UIGeometryObjectModel_Modified(object sender, EventArgs e)
        {
            Load(uIGeometryObjectModel?.JSAMObject);
        }

        private void UIGeometryObjectModel_Opened(object sender, EventArgs e)
        {
            Load(uIGeometryObjectModel?.JSAMObject);
        }

        private void UpdateMode()
        {
            if(mode == Mode.ThreeDimensional)
            {
                helixViewport3D.Camera = new PerspectiveCamera();

                helixViewport3D.Orthographic = false;
                helixViewport3D.ShowViewCube = true;
                helixViewport3D.ShowCoordinateSystem = true;
                helixViewport3D.CameraMode = HelixToolkit.Wpf.CameraMode.Inspect;
                helixViewport3D.ZoomAroundMouseDownPoint = true;
                helixViewport3D.IsPanEnabled = true;
                helixViewport3D.IsRotationEnabled = true;
                //helixViewport3D.ShowCameraInfo = true;

            }
            else
            {
                helixViewport3D.Orthographic = true;
                helixViewport3D.ShowViewCube = false;
                helixViewport3D.ShowCoordinateSystem = false;
                helixViewport3D.Camera.LookDirection = new Vector3D(0, 0.0001, -0.9999);
                helixViewport3D.Camera.NearPlaneDistance = -1000;
                helixViewport3D.Camera.FarPlaneDistance = 1000;
                helixViewport3D.ZoomAroundMouseDownPoint = false;
                helixViewport3D.IsPanEnabled = true;
                helixViewport3D.IsRotationEnabled = false;
                //helixViewport3D.ShowCameraInfo = true;
            }

            helixViewport3D.ZoomExtents();

            if (floorPlan2DEnabled)
            {
                bool active2D = Active2D;

                floorPlan2DControl.Visibility = active2D ? Visibility.Visible : Visibility.Collapsed;
                helixViewport3D.Visibility = active2D ? Visibility.Collapsed : Visibility.Visible;

                // Mode is assigned after UIGeometryObjectModel (see AnalyticalWindow.UpdateTabItem) - reroute the loaded model to the renderer that just became active
                Load(uIGeometryObjectModel?.JSAMObject);
            }
        }
    }
}
