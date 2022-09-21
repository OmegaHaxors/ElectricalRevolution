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
		public double ParasiticResistance = 0; //Resistance converted into heat
		public double Voltage = 0; //in Volts
		public double SeriesCapacitance = double.PositiveInfinity; //in Farads
		public double ParasiticCapacitance = 0; //capacitance that goes to ground
		public double Current = 0; //in Amps
		public double Inductance = 0; //in Henries
    public double Temperature = 60; //in degree(c)
    public BlockPos LeaderNode = null; //who is in charge of this joint?
    public BlockPos[] NodeList = new BlockPos[0];

		public BEBehaviorElectricalNode(BlockEntity blockentity) : base(blockentity){}

    public override void Initialize(ICoreAPI api, JsonObject properties)
		{ //runs when the block is created or loaded
      if(LeaderNode == null){LeaderNode = this.Blockentity.Pos;}
      base.Initialize(api, properties);
      this.Blockentity.MarkDirty(); //makes sure that any remote information is updated to the block
      AddToOrUpdateBlockMap();
		}

    public void AddToOrUpdateBlockMap()
    {
      Dictionary<BlockPos, BEBehaviorElectricalNode> blockmap = Api.ModLoader.GetModSystem<ELR>().blockmap; //thanks to G3rste#1850 for this trick.
      if(!blockmap.ContainsKey(this.Blockentity.Pos)){blockmap.Add(this.Blockentity.Pos,this);}//don't add if it already exists
      else{//update the old object if an entry already exists
        DownloadDataFromBlockmap();
        blockmap[this.Blockentity.Pos] = this;
      }
    }
    public void DownloadDataFromBlockmap()
    {//pulls information from the blockmap and puts it into this object
      BEBehaviorElectricalNode node = Api.ModLoader.GetModSystem<ELR>().blockmap[this.Blockentity.Pos];
      this.LeaderNode = node.LeaderNode;
      this.Current = node.Current;
      this.Inductance = node.Inductance;
      this.Resistance = node.Resistance;
      this.NodeList = node.NodeList;
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
        /*public void TryToFindNodes() //depricated. This system sucks.
    {
      //check each face for neighbours (Up,Down,North,East,South,West)
      BlockPos blockpos = this.Blockentity.Pos;
      BlockPos[] neighbourposes = new BlockPos[3]{
        blockpos.UpCopy(1),
        //blockpos.DownCopy(1),
        blockpos.NorthCopy(1),
        blockpos.EastCopy(1),
        //blockpos.SouthCopy(1),
        //blockpos.WestCopy(1),
      };
      foreach(BlockPos neighbourpos in neighbourposes)
      {
        if(this.LeaderLocation == this.Blockentity.Pos)//You're a Leader. Pawn off responsiblity to anyone who will take it.
        { //You're a Leader
          BlockEntity updog = this.Blockentity.Api.World.BlockAccessor.GetBlockEntity(neighbourpos);
          if(updog == null){continue;} //move on to the next iteration if nothing's there
          Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/anvilhit"),neighbourpos.X,neighbourpos.Y,neighbourpos.Z);
          BEBehaviorElectricalNode neighbournode = updog.GetBehavior<BEBehaviorElectricalNode>();
          if(neighbournode == null){continue;}
          if(neighbournode.ConnectedNodes > 0) //Tests if the neighbour is also a Leader
          { //they're a leader
            if(ShouldIBeTheLeader(blockpos,neighbourpos))
            {
              this.ConnectedNodes =+ neighbournode.ConnectedNodes; neighbournode.ConnectedNodes = 0; //absorb the node's soul
              neighbournode.LeaderLocation = this.Blockentity.Pos; //tell them to follow you as leader
              this.Blockentity.MarkDirty(true);
              neighbournode.Blockentity.MarkDirty(true);
            }else
            {//The other block should be the leader
              neighbournode.ConnectedNodes =+ this.ConnectedNodes; this.ConnectedNodes = 0; //give up your soul
              this.LeaderLocation = neighbournode.Blockentity.Pos; //follow them
              this.Blockentity.MarkDirty(true);
              neighbournode.Blockentity.MarkDirty(true);
            }
          }else //they have a leader. Give up your posessions and follow them.
          {//check to see if their leader is even loaded in the first place, if not, become the leader
            BlockEntity leaderBE = Api.World.BlockAccessor.GetBlockEntity(neighbournode.LeaderLocation);
            if(leaderBE == null){continue;} //check if it's null first
            BEBehaviorElectricalNode leadernode = Api.World.BlockAccessor.GetBlockEntity(neighbournode.LeaderLocation).GetBehavior<BEBehaviorElectricalNode>();
            if(leadernode == null){continue;} //make sure someone didn't do the ol switcheroo
            this.LeaderLocation = neighbournode.LeaderLocation; //You're now following the new leader
            leadernode.ConnectedNodes += this.ConnectedNodes; //give up your soul to the leader
            this.Blockentity.MarkDirty(true);
            leadernode.Blockentity.MarkDirty(true);
          }
        }else //You're a Recruiter. Recruit some nodes for your Leader.
        {
          //it's not even finished
        }
      }
    }

    public bool ShouldIBeTheLeader(BlockPos poslocal, BlockPos posremote)
    { //tiebreaker function for when two leaders meet. Closest to 0,0,0 wins.
      if(poslocal.X < posremote.X){return true;}
      if(poslocal.X > posremote.X){return false;}
      //in most cases, it will resolve here, but in the case of a tie...
      if(poslocal.Y < posremote.Y){return true;}
      if(poslocal.Y > posremote.Y){return false;}
      //gee golly, still not resolved? 
      if(poslocal.Z < posremote.Z){return true;}
      if(poslocal.Z > posremote.Z){return false;}
      throw new ArgumentException//something clearly went wrong. Throw an exception.
      ("ShouldIBeTheLeader was unable to resolve. It's likely because the local and remote position were the same.");
    }*/
	}
}