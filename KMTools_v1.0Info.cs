using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace KMTools_v1._0
{
    public class KMTools_v1__0Info : GH_AssemblyInfo
    {
        public override string Name => "KMTools_v1.0";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("71e034f5-0e41-4cfe-87fb-7b82e6f7cde7");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";

        //Return a string representing the version.  This returns the same version as the assembly.
        public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
    }
}