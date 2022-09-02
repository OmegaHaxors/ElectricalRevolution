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
using System.Text;
using SpiceSharp;
using SpiceSharp.Components;
using SpiceSharp.Simulations;

namespace ElectricalRevolution
{
	public class BEBehaviorElectricalNode : BlockEntityBehavior
	{
    public float Power => Voltage * Current;//in Joules
    public float Resistance; //in Ohms
		public float ParasiticResistance; //Resistance converted into heat
		public float Voltage; //in Volts
		public float SeriesCapacitance; //in Farads
		public float ParasiticCapacitance; //capacitance that goes to ground
		public float Current; //in Amps
		public float Inductance; //in Henries
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
			foreach (var meter in meters)
			{
				meters[meter.Key] = isCreativePlayer || HasAttribute(forPlayer, meter.Key);
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
			tree.SetFloat("voltage",Voltage);
			tree.SetFloat("current",Current);
			base.ToTreeAttributes(tree);
		}
	}
}