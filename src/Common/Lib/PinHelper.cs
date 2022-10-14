using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using VSSurvivalMod;
using Vintagestory.GameContent;
using Vintagestory.GameContent.Mechanics;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace ElectricalRevolution
{
    public class PinHelper {
        ///Converts blockpos and subblockpos into a pin
        public static string GetPinNameAtPosition(BlockPos blockpos, Vec3i subblockpos)
        {
            if(blockpos == null || subblockpos == null){return "0";}
            string returnstring = "" + blockpos + " (" + subblockpos.X + ", " + subblockpos.Y + ", "+ subblockpos.Z + ")";
            return returnstring;
        }

        ///Converts a pinpos and a pinneg into a nodename
        public static string GetNodeNameFromPins(string componenttype, string pinnamepos, string pinnameneg)
        {
            string returnstring = componenttype + ":" + pinnamepos + "~" + pinnameneg;
            return returnstring;
        }

        /// Gets the Node type from a node's name
        public static string GetNodeTypeFromName(string nodename)
        {
            string returnstring = nodename;
            int stopat = returnstring.IndexOf(":");
            return returnstring.Substring(0, stopat);
        }

        ///Extracts the positive pin from a node's name
        public static string GetPositivePin(string nodename)
        {
            string returnstring = nodename;
            int startat = returnstring.IndexOf(":") + 1;
            int stopat = returnstring.IndexOf("~");
            int length = stopat-startat;
            return returnstring.Substring(startat, length);
        }

        ///Extracts the negative pin from the node's name
        public static string GetNegativePin(string nodename)
        {
            string returnstring = nodename;
            int startat = returnstring.IndexOf("~") + 1;
            return returnstring.Substring(startat);
        }

        ///Converts a pin into a blockpos
        public static BlockPos PinToBlockPos(string pinname,out Vec3i sublocation)
        {
            string workstring = pinname;
            //the pin name will look like this:
            //10, 3, 10 (0, 0, 0)
            int startat = 0;
            int stopat = workstring.IndexOf(",");
            int length = stopat-startat;
            int x = workstring.Substring(startat,length).ToInt(-666);
            workstring = workstring.Substring(stopat+2); //2 gets rid of the comma and space
            //3, 10 (0, 0, 0)
            stopat = workstring.IndexOf(",");
            length = stopat-startat;
            int y = workstring.Substring(startat,length).ToInt(-666);
            workstring = workstring.Substring(stopat+2); //2 gets rid of the comma and space
            //should be 10 (0, 0, 0)
            stopat = workstring.IndexOf("(")-1;
            length = stopat-startat;
            int z = workstring.Substring(startat,length).ToInt(-666);
            workstring = workstring.Substring(stopat+2); //get rid of the space and )
            //0, 0, 0)
            stopat = workstring.IndexOf(",");
            length = stopat-startat;
            int sex = workstring.Substring(startat,length).ToInt(-666); //sub-x. What the E stands for is as much of a mystery as what the N in ELN stands for.
            workstring = workstring.Substring(stopat+2);
            //should be 0, 0)
            stopat = workstring.IndexOf(",");
            length = stopat-startat;
            int sey = workstring.Substring(startat,length).ToInt(-666);
            workstring = workstring.Substring(stopat+2);
            //should be 0)
            stopat = workstring.IndexOf(")");
            length = stopat-startat;
            int sez = workstring.Substring(startat,length).ToInt(-666);
            workstring = workstring.Substring(stopat+1); //should be empty now

            sublocation = new Vec3i(sex,sey,sez);
            //sapi.BroadcastMessageToAllGroups("x:"+x+"y:"+y+"z:"+z+ ": "+workstring + " subblock:" + sublocation.ToString(),EnumChatType.CommandError);
            //x:11y:3z:10:  subblock:X=1,Y=0,Z=0

            return new BlockPos(x,y,z);
        }
    }
}
