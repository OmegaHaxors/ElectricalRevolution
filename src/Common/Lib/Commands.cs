using System;
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
using System.Collections.Generic;

namespace ElectricalRevolution
{
    public class Commands {
        public static void registerCommands(ICoreAPI api, ICoreServerAPI sapi, ELR elr)
        {
            registerMnaCommand(sapi, elr);
            registerBlocklistCommand(api, sapi, elr);
            registerHereCommand(sapi);
        }

        private static void registerMnaCommand(ICoreServerAPI sapi, ELR elr)
        {
            sapi.RegisterCommand("mna","Gets a readout of the leaderlist","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
                string message = "";
                foreach(BlockPos leaderpos in elr.leadermap)
                {
                    message = message + leaderpos + "[ ";
                    BlockPos[] recruiterlist = elr.blockmap[leaderpos].NodeList;
                    foreach(BlockPos recruiterpos in recruiterlist)
                    {
                        message = message + recruiterpos + "  ";
                    }
                    message = message + "]";
                }
                sapi.SendMessage(splayer,GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
                /*if(args.Length > 0){
            Double.TryParse(args[0],out double dcsetting);
            ckt.TryGetEntity(GetNodeNameFromPins("VoltageSource",GetPinNameAtPosition(new BlockPos(10,3,10),new Vec3i(0,0,0)),"0"), out SpiceSharp.Entities.IEntity component);
            if(!component.TrySetParameter("dc",dcsetting)){throw new NullReferenceException("MNA command couldn't set the parameter: dc");}
            }

            string capacitorname = GetNodeNameFromPins("Capacitor",GetPinNameAtPosition(new BlockPos(13,3,10),new Vec3i(0,0,0)),"0");
            string inductorname = GetNodeNameFromPins("Inductor",GetPinNameAtPosition(new BlockPos(12,3,10),new Vec3i(0,0,0)),GetPinNameAtPosition(new BlockPos(13,3,10),new Vec3i(0,0,0)));
            //double inductorreadout = ICWatchers[inductorname].Value;
            //double capacitorreadout = ICWatchers[capacitorname].Value;
            ckt.TryGetEntity(capacitorname, out SpiceSharp.Entities.IEntity node);
            if(!node.TryGetProperty("ic", out double nodeic)){throw new NullReferenceException("MNA command couldn't get the property: ic");}
            double capacitorreadout = nodeic;
            ckt.TryGetEntity(inductorname, out node);
            if(!node.TryGetProperty("ic", out nodeic)){throw new NullReferenceException("MNA command couldn't get the property: ic");}
            double inductorreadout = nodeic;
            sapi.BroadcastMessageToAllGroups("capacitor: "+capacitorreadout+" inductor: "+inductorreadout,EnumChatType.CommandSuccess);
            */
            }, Privilege.chat);
        }

        private static void registerBlocklistCommand(ICoreAPI api, ICoreServerAPI sapi, ELR elr)
        {
            sapi.RegisterCommand("blocklist","Read out the block list","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
                double inputvalue = 0;
                if(args.Length > 0){
                    Double.TryParse(args[0],out inputvalue);
                }
                foreach(KeyValuePair<BlockPos,BEBehaviorElectricalNode> entry in elr.blockmap)
                {
                    string message = "Block at: " + entry.Key;
                    message = message + " Internal hash: " + entry.Value.Blockentity?.GetHashCode().ToString();
                    message = message + " External hash: " + api.World.BlockAccessor.GetBlockEntity(entry.Value.Blockentity.Pos)?.GetHashCode().ToString();
                    message = message + " Resistance: " + entry.Value.Resistance;
                    entry.Value.Resistance = inputvalue;
                    sapi.SendMessage(splayer,GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
                    entry.Value.Blockentity.MarkDirty(true);
                }
            }, Privilege.chat);
        }

        private static void registerHereCommand(ICoreServerAPI sapi)
        {
            sapi.RegisterCommand("here","Where am I? answered in text form","",(IServerPlayer splayer, int groupId, CmdArgs args) =>
            {
                BlockPos blockpos = splayer.Entity.Pos.AsBlockPos;
                Vec3i subblockpos = new Vec3i(0,0,0);

                string message = PinHelper.GetPinNameAtPosition(blockpos,subblockpos);
                string nodename = PinHelper.GetNodeNameFromPins("VoltageSource", PinHelper.GetPinNameAtPosition(blockpos,subblockpos), "0");
                string nodetype = PinHelper.GetNodeTypeFromName(nodename);
                string pospinname = PinHelper.GetPositivePin(nodename);
                string negpinname = PinHelper.GetNegativePin(nodename);
                splayer.SendMessage(GlobalConstants.GeneralChatGroup,message,EnumChatType.CommandSuccess);
                splayer.SendMessage(GlobalConstants.GeneralChatGroup,nodename,EnumChatType.CommandSuccess);
                splayer.SendMessage(GlobalConstants.GeneralChatGroup,nodetype,EnumChatType.CommandSuccess);
                splayer.SendMessage(GlobalConstants.GeneralChatGroup,pospinname,EnumChatType.CommandSuccess);
                splayer.SendMessage(GlobalConstants.GeneralChatGroup,negpinname,EnumChatType.CommandSuccess);
            }, Privilege.chat);
        }
    }
}