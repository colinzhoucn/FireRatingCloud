#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion // Namespaces

namespace FireRatingCloud
{
  /// <summary>
  /// Import updated FireRating parameter values 
  /// from external database.
  /// </summary>
  [Transaction( TransactionMode.Manual )]
  public class Cmd_3_ImportSharedParameterValues
    : IExternalCommand
  {
    /// <summary>
    /// Test retrieving only recently modified records.
    /// </summary>
    static uint _test_newer_timestamp = 0;

    /// <summary>
    /// Update the BIM by retrieving database records 
    /// and applying the changes.
    /// </summary>
    public static bool UpdateBimFromDb(
      Document doc,
      uint timestamp,
      ref string error_message )
    {
      Guid paramGuid;

      if ( !Util.GetSharedParamGuid( doc.Application,
        out paramGuid ) )
      {
        error_message = "Shared parameter GUID not found.";
        return false;
      }

      // Determine custom project identifier.

      string project_id = Util.GetProjectIdentifier( doc );

      Stopwatch stopwatch = new Stopwatch();
      stopwatch.Start();

      // Retrieve all doors referencing this project, 
      // optionally modified after the given timestamp.

      List<FireRating.DoorData> doors 
        = BimUpdater.GetDoorRecords( 
          project_id, timestamp );

      // Loop through the doors and update   
      // their firerating parameter values.

      if ( null != doors && 0 < doors.Count )
      {
        using ( Transaction t = new Transaction( doc ) )
        {
          t.Start( "Import Fire Rating Values" );

          // Retrieve element unique id and 
          // FireRating parameter values.

          foreach ( FireRating.DoorData d in doors )
          {
            string uid = d._id;
            Element e = doc.GetElement( uid );

            if ( null == e )
            {
              error_message = string.Format(
                "Error retrieving element for "
                + "unique id {0}.", uid );

              return false;
            }

            Parameter p = e.get_Parameter( paramGuid );

            if ( null == p )
            {
              error_message = string.Format(
                "Error retrieving shared parameter on "
                + " element with unique id {0}.", uid );

              return false;
            }
            object fire_rating = d.firerating;

            p.Set( (double) fire_rating );

            p = e.get_Parameter( DoorData.BipMark );

            if ( null == p )
            {
              error_message = string.Format(
                "Error retrieving ALL_MODEL_MARK "
                + "built-in parameter on element with "
                + "unique id {0}.", uid );

              return false;
            }

            p.Set( (string) d.tag );
          }
          t.Commit();
        }
      }

      stopwatch.Stop();

      Debug.Print(
        "{0} milliseconds to import {1} elements.",
        stopwatch.ElapsedMilliseconds, doors.Count );

      return true;
    }

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      Application app = uiapp.Application;
      Document doc = uiapp.ActiveUIDocument.Document;

      return UpdateBimFromDb( doc, _test_newer_timestamp, ref message )
        ? Result.Succeeded
        : Result.Failed;
    }
  }
}
