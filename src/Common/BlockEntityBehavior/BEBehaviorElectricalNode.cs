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
		public float Power
		{get{return Voltage * Current;}}//in Joules
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
			ItemStack helditem = forPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
			ItemStack offhanditem = forPlayer.Entity.LeftHandItemSlot.Itemstack;
			bool checkrighthand = false; bool checklefthand = false; ITreeAttribute rightattributes = null; ITreeAttribute leftattributes = null;
			bool voltmeter = false; bool ammeter = false; bool calculator = false; bool ohmmeter = false; bool faradmeter = false; bool henrymeter = false; bool thermometer = false;

			if(forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
			{
			voltmeter=ammeter=calculator=ohmmeter=faradmeter=henrymeter=thermometer = true;
			}else{
			if(helditem?.Attributes != null) {checkrighthand = true; rightattributes = helditem.Attributes;}
			if(offhanditem?.Attributes != null) {checklefthand = true; leftattributes = offhanditem.Attributes;}
			if((checkrighthand && rightattributes.HasAttribute("voltmeter"))||(checklefthand && leftattributes.HasAttribute("voltmeter"))){voltmeter = true;}
			if((checkrighthand && rightattributes.HasAttribute("ammeter"))||(checklefthand && leftattributes.HasAttribute("ammeter"))){ammeter = true;}
			if((checkrighthand && rightattributes.HasAttribute("calculator"))||(checklefthand && leftattributes.HasAttribute("calculator"))){calculator = true;}
			if((checkrighthand && rightattributes.HasAttribute("ohmmeter"))||(checklefthand && leftattributes.HasAttribute("ohmmeter"))){ohmmeter = true;}
			if((checkrighthand && rightattributes.HasAttribute("faradmeter"))||(checklefthand && leftattributes.HasAttribute("faradmeter"))){faradmeter = true;}
			if((checkrighthand && rightattributes.HasAttribute("henrymeter"))||(checklefthand && leftattributes.HasAttribute("henrymeter"))){henrymeter = true;}
			if((checkrighthand && rightattributes.HasAttribute("thermometer"))||(checklefthand && leftattributes.HasAttribute("thermometer"))){thermometer = true;}
			}
			if(voltmeter){sb.AppendLine(string.Format(Lang.Get("Node: {0}V", new object[]{this.Voltage}), Array.Empty<object>()));}
			if(ammeter){sb.AppendLine(string.Format(Lang.Get("Node: {0}A", new object[]{this.Current}), Array.Empty<object>()));}
			if(voltmeter&&ammeter&&calculator){sb.AppendLine(string.Format(Lang.Get("Node: {0}W", new object[]{this.Voltage * this.Power}), Array.Empty<object>()));}
			if(ohmmeter){sb.AppendLine(string.Format(Lang.Get("Node: {0}Ω", new object[]{this.Resistance}), Array.Empty<object>()));}
			if(faradmeter){sb.AppendLine(string.Format(Lang.Get("Node: {0}F", new object[]{this.SeriesCapacitance}), Array.Empty<object>()));}
			if(henrymeter){sb.AppendLine(string.Format(Lang.Get("Node: {0}H", new object[]{this.Inductance}), Array.Empty<object>()));}

			if(ohmmeter&&ammeter&&calculator){sb.AppendLine(string.Format(Lang.Get("Node: Parasitic {0}Ω", new object[]{this.ParasiticResistance}), Array.Empty<object>()));}
			if(faradmeter&&voltmeter&&calculator){sb.AppendLine(string.Format(Lang.Get("Node: Parasitic {0}F", new object[]{this.ParasiticCapacitance}), Array.Empty<object>()));}
			if(thermometer){sb.AppendLine(string.Format(Lang.Get("Node: {0}°c", new object[]{"0"}), Array.Empty<object>()));}
			if(!voltmeter&&!ammeter&&!ohmmeter&&!faradmeter&&!henrymeter&&!thermometer){sb.AppendLine(string.Format(Lang.Get("Hold a meter tool in either hand to get a readout."), Array.Empty<object>()));}
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