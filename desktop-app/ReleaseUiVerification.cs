using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

internal static class ReleaseUiVerification
{
    public static int Run()
    {
        string artifacts=Path.Combine(SearchRunner.Root,"verification","layout-accessibility");Directory.CreateDirectory(artifacts);
        var notes=new List<string>();
        foreach(int percent in new[]{100,125,150})using(var app=new AppShell()){
            app.Show();Application.DoEvents();float scale=percent/100f/(app.DeviceDpi/96f);app.Scale(new SizeF(scale,scale));app.PerformLayout();Application.DoEvents();
            foreach(var entry in new[]{new{Key=Keys.D1,Page="Search"},new{Key=Keys.D2,Page="Live Monitor"},new{Key=Keys.D3,Page="Compare"},new{Key=Keys.D4,Page="Benchmark Lab"},new{Key=Keys.D5,Page="Duplicates"},new{Key=Keys.D6,Page="Settings"}}){
                if(!app.NavigateKey(Keys.Alt|entry.Key)||app.CurrentPage!=entry.Page)throw new Exception("Keyboard navigation failed.");
                app.PerformLayout();Application.DoEvents();
                using(var image=new Bitmap(app.Width,app.Height)){app.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(artifacts,percent+"-"+entry.Page.Replace(" ","-")+".png"));}
            }
            app.NavigateKey(Keys.Control|Keys.F);Application.DoEvents();if(ActiveLeaf(app)!=app.Search.Options.Query)throw new Exception("Find shortcut did not select query as keyboard focus target.");
            app.NavigateKey(Keys.Control|Keys.L);Application.DoEvents();if(ActiveLeaf(app)!=app.Search.Options.Folder)throw new Exception("Location shortcut did not select folder as keyboard focus target.");
            if(app.Search.Options.Query.AccessibilityObject.Name!="Query / Pattern"||app.Search.Options.Folder.AccessibilityObject.Name!="Search Location"||app.Search.Grid.AccessibilityObject.Name!="Search result files")throw new Exception("Accessible names are missing.");
            notes.Add("PASS simulated "+percent+"% layout: all six pages, Alt+1..6 navigation, Ctrl+F/Ctrl+L focus, accessible query/location/result names; native device DPI="+app.DeviceDpi);
            app.Close();
        }
        notes.Add("Simulation is not physical mixed-monitor DPI switching or a screen-reader user acceptance test.");File.WriteAllLines(Path.Combine(artifacts,"checks.txt"),notes);return 0;
    }
    private static Control ActiveLeaf(ContainerControl container){Control active=container.ActiveControl;while(active is ContainerControl && ((ContainerControl)active).ActiveControl!=null)active=((ContainerControl)active).ActiveControl;return active;}
}
