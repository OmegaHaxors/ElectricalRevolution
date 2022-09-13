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

namespace ElectricalRevolution
{
	public class BEBehaviorElectricalNode : BlockEntityBehavior
	{
    public double Power => Voltage * Current;//in Joules
    public double Resistance; //in Ohms
		public double ParasiticResistance; //Resistance converted into heat
		public double Voltage; //in Volts
		public double SeriesCapacitance; //in Farads
		public double ParasiticCapacitance; //capacitance that goes to ground
		public double Current; //in Amps
		public double Inductance; //in Henries
		public BEBehaviorElectricalNode(BlockEntity blockentity) : base(blockentity){}

    public override void Initialize(ICoreAPI api, JsonObject properties)
		{
			Resistance = 0; ParasiticResistance = 0; ParasiticCapacitance = 0;
			Voltage = 0;  SeriesCapacitance = float.PositiveInfinity;
			Current = 0; Inductance = 0;
			base.Initialize(api, properties);
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
        AppendMeasurementText(sb, "Node: {0}W", Voltage * Power);
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
        AppendMeasurementText(sb, "Node: {0}°c", "0");
      }
      if (!meters["voltmeter"] && !meters["ammeter"] && !meters["ohmmeter"] && !meters["faradmeter"] && !meters["henrymeter"] && !meters["thermometer"])
      {
        sb.AppendFormat(Lang.Get("Hold a meter tool in either hand to get a readout."), Array.Empty<object>()).AppendLine();
      }
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

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
		{
			Voltage = tree.GetFloat("voltage");
			Current = tree.GetFloat("current");
			base.FromTreeAttributes(tree, world);
		}
		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			tree.SetDouble("voltage",Voltage);
			tree.SetDouble("current",Current);
			base.ToTreeAttributes(tree);
		}
	}
}