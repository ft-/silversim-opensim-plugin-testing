// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3

using SilverSim.Viewer.Messages.LayerData;
using System.Collections.Generic;
using System.IO;

namespace SilverSim.OpenSimArchiver.RegionArchiver
{
    public static partial class OAR
    {
        static class TerrainLoader
        {
            public static List<LayerPatch> LoadStream(Stream input, int suggested_width, int suggested_height)
            {
                List<LayerPatch> patches = new List<LayerPatch>();
                float[,] vals = new float[LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES, suggested_width];

                using (BinaryReader bs = new BinaryReader(input))
                {
                    for (uint patchy = 0; patchy < suggested_height / LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++patchy)
                    {
                        /* we have to load 16 lines at a time */
                        for (uint liney = 0; liney < LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++liney)
                        {
                            for (uint x = 0; x < suggested_width; ++x)
                            {
                                vals[liney, x] = bs.ReadSingle();
                            }
                        }

                        /* now build patches from those 16 lines */
                        for (uint patchx = 0; patchx < suggested_width / LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++patchx)
                        {
                            LayerPatch patch = new LayerPatch();
                            patch.X = patchx;
                            patch.Y = patchy;
                            for (uint y = 0; y < LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++y)
                            {
                                for (uint x = 0; x < LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES; ++x)
                                {
                                    patch[x, y] = vals[y, x + patchx * LayerCompressor.LAYER_PATCH_NUM_XY_ENTRIES];
                                }
                            }
                            patches.Add(patch);
                        }
                    }
                }

                return patches;
            }
        }
    }
}
