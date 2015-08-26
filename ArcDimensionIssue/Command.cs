#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace ArcDimensionIssue
{
  [TransactionAttribute( TransactionMode.Manual )]
  public class Command : IExternalCommand
  {
    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiApp = commandData.Application;
      Autodesk.Revit.Creation.Application app = uiApp.Application.Create;

      Document familyDoc = uiApp.Application.NewFamilyDocument(
        "Z:/a/case/sfdc/10897796/attach/Metric Mechanical Equipment.rft" );

      //View view = getView(familyDocument);

      Family family = familyDoc.OwnerFamily;
      Autodesk.Revit.Creation.FamilyItemFactory factory = familyDoc.FamilyCreate;
      Extrusion extrusion = null;

      // Create an extrusion

      using( Transaction trans = new Transaction( familyDoc ) )
      {
        trans.Start( "Create Extrusion" );

        XYZ arcCenter = new XYZ( 0.0, 3.0, -2.0 );
        double arcRadius = 1.0;
        Arc arc = Arc.Create( arcCenter, arcRadius, 0.0, 2 * Math.PI, XYZ.BasisZ, XYZ.BasisY.Negate() );

        CurveArray curves = app.NewCurveArray();
        curves.Append( arc );

        CurveArrArray profile = app.NewCurveArrArray();
        profile.Append( curves );

        Plane plane = new Plane( XYZ.BasisX.Negate(), arcCenter );
        SketchPlane sketchPlane = SketchPlane.Create( familyDoc, plane );
        Debug.WriteLine( "Origin: " + sketchPlane.GetPlane().Origin );
        Debug.WriteLine( "Normal: " + sketchPlane.GetPlane().Normal );
        Debug.WriteLine( "XVec:   " + sketchPlane.GetPlane().XVec );
        Debug.WriteLine( "YVec:   " + sketchPlane.GetPlane().YVec );
        extrusion = factory.NewExtrusion( true, profile, sketchPlane, 10 );

        //familyDoc.Regenerate(); // this is done by Commit anyway

        trans.Commit();
      }

      // Create dimension 

      using( Transaction trans = new Transaction( familyDoc ) )
      {
        trans.Start( "Create Dimension" );

        // the arc's reference from creating the extrusion 
        // is null at this point, so I retrieve it from 
        // the sketch's profile again

        Arc arc = null;
        foreach( CurveArray curArr in extrusion.Sketch.Profile )
        {
          foreach( Curve curCurve in curArr )
          {
            arc = curCurve as Arc;
            break;
          }
        }

        View view = GetView( ViewType.Elevation, XYZ.BasisY.Negate(),
          XYZ.BasisZ, familyDoc );

        ReferencePlane referencePlane = factory.NewReferencePlane(
          new XYZ( 1.0, 0.0, -2.0 ), new XYZ( 0.0, 0.0, -2.0 ),
          new XYZ( 0.0, 1.0, -2.0 ), view );

        ReferenceArray refArray = new ReferenceArray();
        refArray.Append( referencePlane.GetReference() );
        refArray.Append( arc.Reference );

        Line line = Line.CreateUnbound( arc.Center, XYZ.BasisZ );

#if DEBUG
        // Display arc from cylinder

        ModelCurve marc = factory.NewModelCurve( arc, extrusion.Sketch.SketchPlane );
        
        // Display X and Y lines on reference plane
        
        Plane plane = referencePlane.GetPlane();
        SketchPlane sp = SketchPlane.Create( familyDoc, plane );
        XYZ origin = plane.Origin;
        Line linex = Line.CreateBound( origin, origin + plane.XVec );
        ModelCurve mlinex = factory.NewModelCurve( linex, sp );
        Line liney = Line.CreateBound( origin, origin + plane.YVec );
        ModelCurve mliney = factory.NewModelCurve( liney, sp );

        bool create_dimension = false;
        if( create_dimension )
        {
          Dimension dimension = factory.NewDimension( view, line, refArray );
        }
#else
        Dimension dimension = factory.NewDimension( view, line, refArray );
#endif // DEBUG

        //familyDoc.Regenerate(); // this is done by Commit anyway

        trans.Commit();
      }

#if DEBUG
      SaveAsOptions opt = new SaveAsOptions();
      opt.OverwriteExistingFile = true;
      familyDoc.SaveAs( "Z:/a/case/sfdc/10897796/attach/test.rfa", opt );
#endif // DEBUG

      return Result.Succeeded;
    }

    static View GetView( ViewType viewType, XYZ rightDir, XYZ upDir, Document familyDoc )
    {
      FilteredElementCollector collector = new FilteredElementCollector( familyDoc );
      collector.WherePasses( new ElementClassFilter( typeof( View ) ) );

      IEnumerable<View> views =
        from View view in collector
        where view.ViewType == viewType &&
              view.RightDirection.IsAlmostEqualTo( rightDir ) &&
              view.UpDirection.IsAlmostEqualTo( upDir )
        select view;

      if( views.Count<View>() > 0 )
      {
        return views.First<View>();
      }
      return null;
    }
  }
}
