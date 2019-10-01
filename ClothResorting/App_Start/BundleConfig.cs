﻿using System.Web;
using System.Web.Optimization;

namespace ClothResorting
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js",
                        "~/Scripts/DataTables/jquery.dataTables.js",
                        "~/Scripts/DataTables/dataTables.bootstrap.js",
                        "~/Scripts/Customized/GrandChannel.js",
                        "~/Scripts/layer/layer.js",
                        "~/Scripts/bootbox.js",
                        "~/Scripts/jquery.signalR-2.4.1.js",
                        "~/Scripts/serviceWorker.min.js",
                        "~/Scripts/push.js",
                        "~/Scripts/EditableSelect/dist/jquery-editable-select.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js",
                      "~/Scripts/respond.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/site.css",
                      "~/Content/DataTables/css/dataTables.bootstrap.css",
                      "~/Scripts/layer/theme/default/layer.css",
                      "~/Scripts/EditableSelect/dist/jquery-editable-select.min.css"));
        }
    }
}
