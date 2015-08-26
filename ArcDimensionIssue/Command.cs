#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.IO;
#endregion

namespace ArcDimensionIssue
{
  [TransactionAttribute( TransactionMode.Manual )]
  public class Command : IExternalCommand
  {
    //const string _folder = "C:\\Temp\\ArcDimensionIssue";
    //const string _template = "Metric Mechanical Equipment 16.rft";

    const string _folder = "Z:/a/case/sfdc/10897796/attach";
    const string _template = "Metric Mechanical Equipment.rft";

    public Result Execute( 
      ExternalCommandData commandData, 
      ref string message, 
      ElementSet elements )
    {
      UIApplication uiApp = commandData.Application;
      Autodesk.Revit.Creation.Application app = uiApp.Application.Create;

      string filename = Path.Combine( _folder, _template );

      Document familyDoc = uiApp.Application.NewFamilyDocument( filename );

      Family family = familyDoc.OwnerFamily;
      Autodesk.Revit.Creation.FamilyItemFactory factory = familyDoc.FamilyCreate;
      Extrusion extrusion = null;

      using( Transaction trans = new Transaction( familyDoc ) )
      {
        trans.Start( "Create Extrusion" );

        XYZ arcCenter = new XYZ( 0.0, 3.0, -2.0 );

        // Get the "left" view

        View view = GetView( ViewType.Elevation, XYZ.BasisY.Negate(),
          XYZ.BasisZ, familyDoc );

        // 2D offsets from view 'left'

        XYZ xOffset = new XYZ( 0.0, 10.0, 0.0 );
        XYZ yOffset = new XYZ( 0.0, 0.0, -10.0 );

        //################## Reference planes ################################

        // Origin's reference planes
        
        ReferencePlane referencePlaneOriginX = factory.NewReferencePlane(
          new XYZ( 1.0, 0.0, 0.0 ),
          new XYZ( 0.0, 0.0, 0.0 ),
          new XYZ( 0.0, 1.0, 0.0 ), view );
        referencePlaneOriginX.Name = "ReferencePlane_OriginX";
        referencePlaneOriginX.Pinned = true;

        ReferencePlane referencePlaneOriginY = factory.NewReferencePlane(
          new XYZ( 0.0, 0.0, 1.0 ),
          new XYZ( 0.0, 0.0, 0.0 ),
          new XYZ( -1.0, 0.0, 0.0 ), view );
        referencePlaneOriginY.Name = "ReferencePlane_OriginY";
        referencePlaneOriginY.Pinned = true;

        // Reference planes the extruded arc should stick to

        ReferencePlane referencePlaneX = factory.NewReferencePlane(
          new XYZ( 1.0, 0.0, 0.0 ) + yOffset,
          new XYZ( 0.0, 0.0, 0.0 ) + yOffset,
          new XYZ( 0.0, 1.0, 0.0 ), view );
        referencePlaneX.Name = "ReferencePlane_X";

        ReferencePlane referencePlaneY = factory.NewReferencePlane(
          new XYZ( 0.0, 0.0, 1.0 ) + xOffset,
          new XYZ( 0.0, 0.0, 0.0 ) + xOffset,
          new XYZ( -1.0, 0.0, 0.0 ), view );
        referencePlaneY.Name = "ReferencePlane_Y";

        familyDoc.Regenerate();

        //################## Create dimensions ###############################
        
        // Dimension x

        ReferenceArray refArrayX = new ReferenceArray();
        refArrayX.Append( referencePlaneX.GetReference() );
        refArrayX.Append( referencePlaneOriginX.GetReference() );

        Line lineX = Line.CreateUnbound( arcCenter, XYZ.BasisZ );
        Dimension dimensionX = factory.NewDimension( view, lineX, refArrayX );

        // Dimension y

        ReferenceArray refArrayY = new ReferenceArray();
        refArrayY.Append( referencePlaneY.GetReference() );
        refArrayY.Append( referencePlaneOriginY.GetReference() );

        Line lineY = Line.CreateUnbound( arcCenter, XYZ.BasisY );
        Dimension dimensionY = factory.NewDimension( view, lineY, refArrayY );

        familyDoc.Regenerate();

        //################## Create arc ######################################

        double arcRadius = 1.0;
        Arc arc = Arc.Create( new XYZ( 0.0, 0.0, 0.0 ) + xOffset + yOffset,
          arcRadius, 0.0, 2 * Math.PI, XYZ.BasisZ, XYZ.BasisY.Negate() );

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

        familyDoc.Regenerate();

        //################## Add family parameters ###########################

        FamilyParameter paramX = familyDoc.FamilyManager.AddParameter( "X",
          BuiltInParameterGroup.PG_GEOMETRY, ParameterType.Length, true );
        dimensionX.FamilyLabel = paramX;

        FamilyParameter paramY = familyDoc.FamilyManager.AddParameter( "Y",
          BuiltInParameterGroup.PG_GEOMETRY, ParameterType.Length, true );
        dimensionY.FamilyLabel = paramY;

        // Set their values

        FamilyType familyType = familyDoc.FamilyManager.NewType( "Standard" );
        familyDoc.FamilyManager.Set( paramX, 10 );
        familyDoc.FamilyManager.Set( paramY, 10 );

        trans.Commit();
      }

      // Save it to inspect

      SaveAsOptions opt = new SaveAsOptions();
      opt.OverwriteExistingFile = true;

      filename = Path.Combine( Path.GetTempPath(), "test.rfa" );

      familyDoc.SaveAs( filename, opt );

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
