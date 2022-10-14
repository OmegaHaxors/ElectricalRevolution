using System;
using System.Linq;
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
using System.Text;
using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.Simulations;
using ElectricalRevolution;
using ProtoBuf;

namespace ElectricalRevolution
{
	[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)] //huge credit to DEREK#0001 for this one
    public class BEBehaviorElectricalNode : BlockEntityBehavior
	{
        public double Resistance = 0; //in Ohms
        public double Voltage = 0; //in Volts
        public double SeriesCapacitance = double.PositiveInfinity; //in Farads
        public double ParasiticCapacitance = 0; //capacitance that goes to ground
        public double ParasiticResistance = 0; //Resistance for the Parasitic Capacitors
        public double Current = 0; //in Amps
        public double Inductance = 0; //in Henries
        public double Temperature = 60; //in degree(c)
        public BlockPos LeaderNode = null; //who is in charge of this joint?
        public BlockPos[] NodeList = new BlockPos[0];

		public BEBehaviorElectricalNode(BlockEntity blockentity) : base(blockentity){}

        ///runs when the block is created or loaded
        public override void Initialize(ICoreAPI api, JsonObject properties)
		{
          if(LeaderNode == null){LeaderNode = this.Blockentity.Pos;}
          if(NodeList.Count() <= 0){NodeList = NodeList.Append<BlockPos>(LeaderNode);}
          base.Initialize(api, properties);
          this.Blockentity.MarkDirty(); //makes sure that any remote information is updated to the block
          AddToOrUpdateBlockMap();
		}

        public void AddToOrUpdateBlockMap()
        {
            //just going to add a temporary component here for testing purposes
            Dictionary<string, SpiceSharp.Entities.IEntity> componentlist = Api.ModLoader.GetModSystem<ELR>().componentlist; //thanks to G3rste#1850 for this trick.
            string resistorname = PinHelper.GetNodeNameFromPins("Resistor",PinHelper.GetPinNameAtPosition(this.Blockentity.Pos,new Vec3i(0,0,0)),PinHelper.GetPinNameAtPosition(this.Blockentity.Pos.NorthCopy(),new Vec3i(0,0,0)));
            componentlist[resistorname] = new Resistor(resistorname,PinHelper.GetPositivePin(resistorname),PinHelper.GetNegativePin(resistorname),1);

            Dictionary<BlockPos, BEBehaviorElectricalNode> blockmap = Api.ModLoader.GetModSystem<ELR>().blockmap; //thanks to G3rste#1850 for this trick.
            if(!blockmap.ContainsKey(this.Blockentity.Pos))
            {
                blockmap.Add(this.Blockentity.Pos,this);
            }//don't add if it already exists
            else
            {//update the old object if an entry already exists
                DownloadDataFromBlockmap();
                blockmap[this.Blockentity.Pos] = this;
            }
        }

        ///pulls information from the blockmap and puts it into this object
        public void DownloadDataFromBlockmap()
        {
            BEBehaviorElectricalNode node = Api.ModLoader.GetModSystem<ELR>().blockmap[this.Blockentity.Pos];
            this.LeaderNode = node.LeaderNode;
            this.Current = node.Current;
            this.Inductance = node.Inductance;
            this.Resistance = node.Resistance;
            if(node.NodeList != null){this.NodeList = node.NodeList;}else{this.NodeList = new BlockPos[0];}
            this.Temperature = node.Temperature;
            this.ParasiticCapacitance = node.ParasiticCapacitance;
            this.ParasiticResistance = node.ParasiticResistance;
            this.SeriesCapacitance = node.SeriesCapacitance;
            this.Voltage = node.Voltage;
        }

        public void RemoveFromBlockMap()
        {
            Dictionary<BlockPos, BEBehaviorElectricalNode> blockmap = Api.ModLoader.GetModSystem<ELR>().blockmap; //thanks to G3rste#1850 for this trick.
            if(blockmap.ContainsKey(this.Blockentity.Pos)){blockmap.Remove(this.Blockentity.Pos);} //only remove if it exists
        }

        public override void OnBlockRemoved() //Only removes if block was removed, not if it was unloaded
        {
            Dictionary<BlockPos, BEBehaviorElectricalNode> blockmap = Api.ModLoader.GetModSystem<ELR>().blockmap; //thanks to G3rste#1850 for this trick.

            //if any blocks get removed, destroy the entire network, as it's no longer valid
            if(this.NodeList.Length > 0) //check if you're a leader
            {//if you are, you can destroy the network yourself.
                foreach(BlockPos recruiterpos in (BlockPos[])NodeList.Clone()) //notify your recruiters
                {
                    blockmap[recruiterpos].LeaderNode = recruiterpos;
                    blockmap[recruiterpos].NodeList = new BlockPos[1]{recruiterpos}; //set them as a self-leader
                }
            }
            else //you're a recruiter. Make your leader do the heavy lifting
            {
                BEBehaviorElectricalNode leadernode = blockmap[LeaderNode];
                foreach(BlockPos recruiterpos in (BlockPos[])leadernode.NodeList.Clone()) //notify your leader's recruiters
                {
                    blockmap[recruiterpos].LeaderNode = recruiterpos;
                    blockmap[recruiterpos].NodeList = new BlockPos[1]{recruiterpos}; //set them as a self-leader
                }
            }
            base.OnBlockRemoved();
            RemoveFromBlockMap();
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
        {
            base.GetBlockInfo(forPlayer, sb);

            Dictionary<string, bool> meters = new Dictionary<string, bool>
            {
                { "voltmeter", false },
                { "ammeter", false },
                { "calculator", false },
                { "ohmmeter", false },
                { "faradmeter", false },
                { "henrymeter", false },
                { "thermometer", false }
            };

            bool isCreativePlayer = forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative;
            Dictionary<string,bool> metersclone = meters.ToDictionary(entry => entry.Key,entry => entry.Value);
            foreach (var meter in metersclone)
            {
                meters[meter.Key] = isCreativePlayer || HasAttribute(forPlayer, meter.Key);
            }

            if (meters["voltmeter"])
            {
                AppendMeasurementText(sb, "Node: {0}V", Voltage);
            }
            if (meters["ammeter"])
            {
                AppendMeasurementText(sb, "Node: {0}A", Current);
            }
            if (meters["voltmeter"] && meters["ammeter"] && meters["calculator"])
            {
                AppendMeasurementText(sb, "Node: {0}W", Voltage * Current);
            }
            if (meters["ohmmeter"])
            {
                AppendMeasurementText(sb, "Node: {0}Ω", Resistance);
            }
            if (meters["faradmeter"])
            {
                AppendMeasurementText(sb, "Node: {0}F", SeriesCapacitance);
            }
            if (meters["henrymeter"])
            {
                AppendMeasurementText(sb, "Node: {0}H", Inductance);
            }
            if (meters["ohmmeter"] && meters["ammeter"] && meters["calculator"])
            {
                AppendMeasurementText(sb, "Node: Parasitic {0}Ω", ParasiticResistance);
            }
            if (meters["faradmeter"] && meters["voltmeter"] && meters["calculator"])
            {
                AppendMeasurementText(sb, "Node: Parasitic {0}F", ParasiticCapacitance);
            }
            if (meters["thermometer"])
            {
                AppendMeasurementText(sb, "Node: {0}°c", Temperature);
            }
            if (!meters["voltmeter"] && !meters["ammeter"] && !meters["ohmmeter"] && !meters["faradmeter"] && !meters["henrymeter"] && !meters["thermometer"])
            {
                sb.AppendFormat(Lang.Get("Hold a meter tool in either hand to get a readout."), Array.Empty<object>()).AppendLine();
            }
            AppendMeasurementText(sb, "Leader Node: {0}",LeaderNode);
            AppendMeasurementText(sb, "Connected Nodes: {0}",NodeList.Length);
        }

        private void AppendMeasurementText(StringBuilder sb, string key, object unit)
        {
            sb.AppendFormat(Lang.Get(key, new object[] { unit }), Array.Empty<object>()).AppendLine();
        }

        public bool HasAttribute(IPlayer player, string treeAttribute)
        {
            var rightHandItem = player.InventoryManager.ActiveHotbarSlot.Itemstack.Attributes;
            var leftHandItem = player.Entity.LeftHandItemSlot.Itemstack.Attributes;
            return rightHandItem.HasAttribute(treeAttribute) || leftHandItem.HasAttribute(treeAttribute);
        }

        public void UpdateTemperature(double temperature)
        {
            //does nothing, yet
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            Voltage = tree.GetDouble("Voltage");
            Current = tree.GetDouble("Current");
            Inductance = tree.GetDouble("Inductance");
            Resistance = tree.GetDouble("Resistance");
            SeriesCapacitance = tree.GetDouble("SeriesCapacitance");
            ParasiticCapacitance = tree.GetDouble("ParasiticCapacitance");
            ParasiticResistance = tree.GetDouble("ParasiticResistance");
            Temperature = tree.GetDouble("Temperature");
            NodeList = SerializerUtil.Deserialize<BlockPos[]>(tree.GetBytes("NodeList"));
            LeaderNode = tree.GetBlockPos("LeaderNode");
            base.FromTreeAttributes(tree, world);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetDouble("Voltage",Voltage);
            tree.SetDouble("Current",Current);
            tree.SetDouble("Inductance",Inductance);
            tree.SetDouble("Resistance",Resistance);
            tree.SetDouble("SeriesCapacitance",SeriesCapacitance);
            tree.SetDouble("ParasiticCapacitance",ParasiticCapacitance);
            tree.SetDouble("ParasiticResistance",ParasiticResistance);
            tree.SetDouble("Temperature",Temperature);
            tree.SetBytes("NodeList",SerializerUtil.Serialize<BlockPos[]>(NodeList));
            tree.SetBlockPos("LeaderNode",LeaderNode);
            base.ToTreeAttributes(tree);
        }
	}
}