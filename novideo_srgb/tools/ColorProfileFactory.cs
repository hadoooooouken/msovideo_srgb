using System.IO;

namespace novideo_srgb
{
    public class ColorProfileFactory
    {
        private static void AddDesc(ICCProfileGenerator profileGenerator, string profileName)
        {
            profileGenerator.AddTag("desc", ICCProfileGenerator.MakeAsciiTag("MHC2 for " + Path.GetFileNameWithoutExtension(profileName)));
            profileGenerator.AddTag("cprt", ICCProfileGenerator.MakeAsciiTag("No copyright, use freely"));
        }

        private static void AddMatrix(ICCProfileGenerator profileGenerator, Colorimetry.ColorSpace target)
        {
            var srgbXYZ = Colorimetry.RGBToPCSXYZ(target);

            profileGenerator.AddTag("rXYZ", ICCProfileGenerator.MakeXYZTag(srgbXYZ[0, 0], srgbXYZ[1, 0], srgbXYZ[2, 0]));
            profileGenerator.AddTag("gXYZ", ICCProfileGenerator.MakeXYZTag(srgbXYZ[0, 1], srgbXYZ[1, 1], srgbXYZ[2, 1]));
            profileGenerator.AddTag("bXYZ", ICCProfileGenerator.MakeXYZTag(srgbXYZ[0, 2], srgbXYZ[1, 2], srgbXYZ[2, 2]));
        }

        private static void AddCurve(ICCProfileGenerator profileGenerator, ToneCurve curve, uint resolution)
        {
            var tagData = ICCProfileGenerator.MakeCurveTag(curve, 1024);
            profileGenerator.AddTag("rTRC", tagData);
            profileGenerator.AddTag("gTRC", tagData);
            profileGenerator.AddTag("bTRC", tagData);
        }

        public static void CreateProfile(string profileName, uint resolution, bool keepWhite, Colorimetry.ColorSpace originColorSpace, Colorimetry.ColorSpace targetColorSpace, Colorimetry.Point white, double gamma)
        {
            var profileGenerator = new ICCProfileGenerator();

            AddDesc(profileGenerator, profileName);

            if (keepWhite)
            {
                profileGenerator.AddTag("wtpt", ICCProfileGenerator.MakeXYZTag(Colorimetry.RGBToXYZ(Colorimetry.D65)));
            }
            else
            {
                profileGenerator.AddTag("wtpt", ICCProfileGenerator.MakeXYZTag(Colorimetry.RGBToXYZ(white)));
            }

            profileGenerator.AddTag("lumi", ICCProfileGenerator.MakeLuminanceTag(80));

            AddMatrix(profileGenerator, targetColorSpace);
            AddCurve(profileGenerator, new GammaToneCurve(gamma), resolution);

            var matrix = Colorimetry.CreateMatrix(originColorSpace, targetColorSpace);
            double[][] luts = new double[][] { new double[] { 0, 1 }, new double[] { 0, 1 }, new double[] { 0, 1 } };

            profileGenerator.AddTag("MHC2", ICCProfileGenerator.MakeMHC2(0, 80, matrix, luts));

            profileGenerator.SaveAs(profileName);
        }

        public static void CreateProfile(string profileName, uint resolution, bool keepWhite, ICCMatrixProfile profile, Colorimetry.ColorSpace targetColorSpace, ToneCurve curve, ToneCurve gamma = null)
        {
            var profileGenerator = new ICCProfileGenerator();

            AddDesc(profileGenerator, profileName);

            if (keepWhite)
            {
                profileGenerator.AddTag("wtpt", ICCProfileGenerator.MakeXYZTag(Colorimetry.RGBToXYZ(Colorimetry.D65)));
            }
            else
            {
                profileGenerator.AddTag("wtpt", ICCProfileGenerator.MakeXYZTag(profile.whitePoint));
            }

            profileGenerator.AddTag("lumi", ICCProfileGenerator.MakeLuminanceTag(profile.luminance));

            AddMatrix(profileGenerator, targetColorSpace);
            AddCurve(profileGenerator, curve, resolution);

            var matrix = Colorimetry.CreateMatrix(profile.matrix, targetColorSpace);
            double[][] luts;

            if (gamma != null)
            {
                luts = new double[3][];
                for (int i = 0; i < 3; i++)
                {
                    luts[i] = new double[resolution];
                    for (int j = 1; j < resolution; j++)
                    {
                        double value = gamma.SampleAt(j / (resolution - 1.0));

                        value = profile.trcs[i].SampleInverseAt(value);

                        if (profile.vcgt != null)
                        {
                            value = profile.vcgt[i].SampleAt(value);
                        }

                        luts[i][j] = value;
                    }
                }
            }
            else
            {
                luts = new double[][] { new double[] { 0, 1 }, new double[] { 0, 1 }, new double[] { 0, 1 } };
            }

            profileGenerator.AddTag("MHC2", ICCProfileGenerator.MakeMHC2(profile.tagBlack * profile.luminance, profile.luminance, matrix, luts));

            profileGenerator.SaveAs(profileName);
        }
    }
}