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

[assembly: ModInfo( "ElectricalRevolution",
	Description = "Bringing the electrical revolution to Vintage Story",
	Website     = "",
	Authors     = new []{ "OmegaHaxors" } )]

namespace ElectricalRevolution
{
	public class ELR: ModSystem
	{
		ICoreAPI api = null;
		ICoreServerAPI sapi = null;
		ICoreClientAPI capi = null;

		public override void Start(ICoreAPI api)
		{
			this.api = api;
			api.RegisterItemClass("ItemMeter",typeof(ItemMeter));
			api.RegisterBlockClass("LightBlock",typeof(LightBlock));
			api.RegisterBlockClass("CreativeSource",typeof(CreativeSource));
			api.RegisterBlockClass("MPGenerator",typeof(MPGenerator));
			api.RegisterBlockEntityClass("LightBlockEntity",typeof(LightBlockEntity));
			api.RegisterBlockEntityClass("CreativeSourceEntity",typeof(CreativeSourceEntity));
			api.RegisterBlockEntityClass("MPGeneratorBlockEntity",typeof(MPGeneratorBlockEntity));
			api.RegisterBlockEntityBehaviorClass("ElectricalNode",typeof(BEBehaviorElectricalNode));
			api.RegisterBlockEntityBehaviorClass("ElectricalConverter",typeof(BEBehaviorElectricalConverter));
			api.RegisterBlockEntityBehaviorClass("MPGenerator", typeof(BEBehaviorMPGenerator));
			api.RegisterBlockEntityBehaviorClass("CreativeConverter",typeof(BEBehaviorCreativeConverter));
		}
		public override void StartClientSide(ICoreClientAPI capi){this.capi = capi;}
		public override void StartServerSide(ICoreServerAPI sapi){this.sapi = sapi;}
	}
	public class LightBlock : Block
	{
		public override byte[] GetLightHsv(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
		{
			if (pos != null)
			{
				LightBlockEntity be = blockAccessor.GetBlockEntity(pos) as LightBlockEntity;
				if (be != null)
				{
					return be.GetLightHsv();
				}
			}
			return base.GetLightHsv(blockAccessor, pos, stack);
		}
		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
			LightBlockEntity LblockEntity = (LightBlockEntity)blockEntity;
			BEBehaviorCreativeConverter behav = (blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorCreativeConverter>() : null;
			if (behav != null)
			{
				bool retvalue = behav.OnInteract(byPlayer);
				//LblockEntity.OnBlockInteract(byPlayer);
				return retvalue;
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}
	}
	public class LightBlockEntity : BlockEntity
	{
		private BEBehaviorElectricalNode nodebehavior = null;
		//private bool powered = false;
		private byte[] lightHsv = new byte[]{7,4,18};
		//Random rred = new Random(765364143);
		//Random rblue = new Random(742543);
		//Random rgreen = new Random(321467);
		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			//RegisterGameTickListener(AdjustLightLevel,1000);//only runs on the server (set to 100)
		}
		public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
		{
			base.CreateBehaviors(block, worldForResolve);
			this.nodebehavior = base.GetBehavior<BEBehaviorElectricalConverter>();
		}
		public byte[] GetLightHsv(){return lightHsv;}
		public void SetLightLevel(byte lightvalue){byte oldvalue = lightHsv[2]; lightHsv[2] = lightvalue;SendLightUpdate();}
		public void SetLightColor(int red, int green, int blue)
		{
			int[] HSVfromRGB = ColorUtil.RgbToHsvInts(red,green,blue);
			lightHsv[0] = (byte)HSVfromRGB[0];
			lightHsv[1] = (byte)HSVfromRGB[1];
			lightHsv[2] = (byte)HSVfromRGB[2];
			SendLightUpdate();
		}
		public void SetLightHsv(byte[] hsv)
		{lightHsv = hsv; SendLightUpdate();}
		public void SendLightUpdate() //do this when you change the light level
		{
			Api.World.BlockAccessor.MarkBlockModified(base.Pos);
		}
		public void SetLightLevelFromVoltage(float voltage = -1f) ///set the light level from the supplied voltage. If no arg, it will take from Tree
		{
			//byte red = (byte)rred.Next(0,22);
			//byte green = (byte)rblue.Next(0,22);
			//byte blue = (byte)rgreen.Next(0,22);
			if(voltage < 0)
			{
			BlockEntity block = Api.World.BlockAccessor.GetBlockEntity(base.Pos);
			voltage = this.GetBehavior<BEBehaviorElectricalConverter>().Voltage;
			}
			SetLightLevel((byte)(voltage * 2.2));
			if(Api.Side == EnumAppSide.Server){Api.World.PlaySoundAt(new AssetLocation("autosifter:sounds/sifterworking"),Pos.X,Pos.Y,Pos.Z);}
		}
	}
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
			ItemStack offhanditem = forPlayer.InventoryManager.GetHotbarItemstack(10);
			bool checkrighthand = false; bool checklefthand = false; ITreeAttribute rightattributes = null; ITreeAttribute leftattributes = null;
			bool voltmeter = false; bool ammeter = false; bool calculator = false; bool ohmmeter = false; bool faradmeter = false; bool henrymeter = false; bool thermometer = false;
			
			if(forPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative)
			{
			voltmeter=ammeter=calculator=ohmmeter=faradmeter=henrymeter=thermometer = true;
			}else{
			if(helditem != null && helditem.Attributes != null){checkrighthand = true; rightattributes = helditem.Attributes;}
			if(offhanditem != null && offhanditem.Attributes != null){checklefthand = true; leftattributes = offhanditem.Attributes;}
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
	public class BEBehaviorElectricalConverter : BEBehaviorElectricalNode
	{
		public float Powerconverted;
		public float Efficiency;
		public BEBehaviorElectricalConverter(BlockEntity blockentity) : base(blockentity){}
		public override void Initialize(ICoreAPI api, JsonObject properties)
		{
			Powerconverted = 0; Efficiency = 1;
			base.Initialize(api, properties);
		}
		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
		{
			base.GetBlockInfo(forPlayer, sb);
			sb.AppendLine(string.Format(Lang.Get("Power Converted: {0}", new object[]
			{
				Powerconverted
			}), Array.Empty<object>()));
		}
		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
		{
			this.Powerconverted = tree.GetFloat("powerconverted", 0);
			base.FromTreeAttributes(tree, world);
		}
		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			tree.SetFloat("powerconverted", this.Powerconverted);
			base.ToTreeAttributes(tree);
		}
	}
	
	public class CreativeSource : Block
	{
		public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
		{
			BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(blockSel.Position);
			BEBehaviorCreativeConverter be = (blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorCreativeConverter>() : null;
			if (be != null)
			{
				return be.OnInteract(byPlayer);
			}
			return base.OnBlockInteractStart(world, byPlayer, blockSel);
		}

	}
	public class CreativeSourceEntity : BlockEntity
	{}
	public class BEBehaviorCreativeConverter: BEBehaviorElectricalConverter
	{
		private float powerSetting;

		public BEBehaviorCreativeConverter(BlockEntity blockentity) : base(blockentity)
		{
			this.powerSetting = 3;
		}
		public override void Initialize(ICoreAPI api, JsonObject properties)
		{base.Initialize(api, properties);}

		internal bool OnInteract(IPlayer byPlayer)
		{
			if(byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack == null){return false;}
			if(!byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.GetName().Contains("Stick")){return false;}
			int num = (int)this.powerSetting + 1;
			if (num > 10){num = 1;}
			this.powerSetting = num;
			this.Blockentity.MarkDirty(true, null);
			Vec3d pos = this.Blockentity.Pos.ToVec3d().Add(0.5, 0.0, 0.5);
			LightBlockEntity lbe = Blockentity as LightBlockEntity;
			if(lbe != null) {lbe.SetLightLevelFromVoltage(this.powerSetting);}
			this.Api.World.PlaySoundAt(new AssetLocation("game:sounds/toggleswitch"), pos.X, pos.Y, pos.Z, byPlayer, false, 16f, 1f);
			return true;
		}
		public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
		{
			base.GetBlockInfo(forPlayer, sb);
			sb.AppendLine(string.Format(Lang.Get("Generator Power: {0}%", new object[]
			{
				10 * this.powerSetting
			}), Array.Empty<object>()));
		}
		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
		{
			this.powerSetting = tree.GetFloat("powerconverted", 0f);
			if (this.powerSetting > 10 || this.powerSetting < 1)
			{
				this.powerSetting = 3;
			}
			base.FromTreeAttributes(tree, world);
		}
		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			base.Voltage = this.powerSetting;
			base.Powerconverted = this.powerSetting;
			base.ToTreeAttributes(tree);
		}
	}
	public class ItemMeter : Item
	{
		//public override void OnLoaded(ICoreAPI api){}
	}
	public class MPGenerator : BlockMPBase
	{
		public bool IsOrientedTo(BlockFacing facing)
		{
			string dirs = base.LastCodePart(0);
			return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
		}
		public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
		{
			return this.IsOrientedTo(face);
		}

		public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
		{
			if (!this.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
			{
				return false;
			}
			foreach (BlockFacing face in BlockFacing.HORIZONTALS)
			{
				BlockPos pos = blockSel.Position.AddCopy(face);
				IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos) as IMechanicalPowerBlock;
				if (block != null && block.HasMechPowerConnectorAt(world, pos, face.Opposite))
				{
					AssetLocation loc = new AssetLocation(base.Code.Domain,base.FirstCodePart(0) + "-" + face.Opposite.Code[0].ToString() + face.Code[0].ToString());
					Block toPlaceBlock = world.GetBlock(loc);
					if (toPlaceBlock == null)
					{
						loc = new AssetLocation(base.Code.Domain, base.FirstCodePart(0) + "-" + face.Code[0].ToString() + face.Opposite.Code[0].ToString());
						toPlaceBlock = world.GetBlock(loc);
					}
					if (toPlaceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
					{
						block.DidConnectAt(world, pos, face.Opposite);
						this.WasPlaced(world, blockSel.Position, face);
						return true;
					}
				}
			}
			if (base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode))
			{
				this.WasPlaced(world, blockSel.Position, null);
				return true;
			}
			return false;
		}

		public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
		{
			BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
			BEBehaviorMPGenerator bempgenerator = (blockEntity != null) ? blockEntity.GetBehavior<BEBehaviorMPGenerator>() : null;
			if (bempgenerator != null && !bempgenerator.IsAttachedToBlock())
			{
				foreach (BlockFacing face in BlockFacing.HORIZONTALS)
				{
					BlockPos npos = pos.AddCopy(face);
					BlockAngledGears blockagears = world.BlockAccessor.GetBlock(npos) as BlockAngledGears;
					if (blockagears != null && blockagears.Facings.Contains(face.Opposite) && blockagears.Facings.Length == 1)
					{
						world.BlockAccessor.BreakBlock(npos, null, 1f);
					}
				}
			}
			base.OnNeighbourBlockChange(world, pos, neibpos);
		}
		public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
		{
		}

	}
	public class MPGeneratorBlockEntity : BlockEntity
	{
		BEBehaviorMPGenerator mpc = null;
		bool powered = false;

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			RegisterGameTickListener(UpdateNBT,10);//only runs on the server (set to 100)
		}
		public void UpdateNBT(float dt)
		{
			this.MarkDirty(true);
		}
		
		public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
		{
			base.CreateBehaviors(block, worldForResolve);
			this.mpc = base.GetBehavior<BEBehaviorMPGenerator>();
			if (this.mpc != null)
			{
				this.mpc.OnConnected = delegate()
				{
					this.powered = true;
					/*if (this.renderer != null)
					{
						this.renderer.ShouldRender = true;
						this.renderer.ShouldRotateAutomated = true;
					}*/
				};
				this.mpc.OnDisconnected = delegate()
				{
					this.powered = false;
					/*if (this.renderer != null)
					{
						this.renderer.ShouldRender = false;
						this.renderer.ShouldRotateAutomated = false;
					}*/
				};
			}
		}
	}
	public class BEBehaviorMPGenerator : BEBehaviorMPConsumer
	{
		public new float resistance = 0f;
		public float powerconverted = 0f;
		public BEBehaviorMPGenerator(BlockEntity blockentity) : base(blockentity)
		{
		}
		public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
		{
			return false;
		}

		public override void Initialize(ICoreAPI api, JsonObject properties)
		{
			base.Initialize(api, properties);
			
			if (api.Side == EnumAppSide.Client)
			{
				this.capi = (api as ICoreClientAPI);
			}
			this.orientations = this.Block.Variant["orientation"];
			string a = this.orientations;
			if (a == "ns")
			{
				this.AxisSign = new int[]
				{
					0,
					0,
					-1
				};
				this.orients[0] = BlockFacing.NORTH;
				this.orients[1] = BlockFacing.SOUTH;
				this.sides[0] = this.Position.AddCopy(BlockFacing.WEST);
				this.sides[1] = this.Position.AddCopy(BlockFacing.EAST);
				return;
			}
			if (!(a == "we"))
			{
				return;
			}
			int[] array = new int[3];
			array[0] = -1;
			this.AxisSign = array;
			this.orients[0] = BlockFacing.WEST;
			this.orients[1] = BlockFacing.EAST;
			this.sides[0] = this.Position.AddCopy(BlockFacing.NORTH);
			this.sides[1] = this.Position.AddCopy(BlockFacing.SOUTH);
		}
		public override float GetResistance()
		{
			//float speed = this.TrueSpeed;
			float theresistance = powerconverted / this.TrueSpeed;
			if(float.IsNaN(theresistance)){theresistance = 0f;}
			return Math.Max(0.01f,theresistance);
		}
		public override void JoinNetwork(MechanicalNetwork network)
		{
			base.JoinNetwork(network);
			float speed = (network == null) ? 0f : (Math.Abs(network.Speed * base.GearedRatio) * 1.6f);
			if (speed > 1f)
			{
				network.Speed /= speed;
				network.clientSpeed /= speed;
			}
		}
		public bool IsAttachedToBlock()
		{
			return (this.orientations == "ns" || this.orientations == "we") && (this.Api.World.BlockAccessor.IsSideSolid(this.Position.X, this.Position.Y - 1, this.Position.Z, BlockFacing.UP) || this.Api.World.BlockAccessor.IsSideSolid(this.Position.X, this.Position.Y + 1, this.Position.Z, BlockFacing.DOWN));
		}
		public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
		{
			MPGeneratorBlockEntity mpgenerator = this.Blockentity as MPGeneratorBlockEntity;
			if(mpgenerator != null)
			{
				if(mpgenerator.GetBehavior<BEBehaviorElectricalConverter>() != null)
				{
					BEBehaviorElectricalConverter converter = mpgenerator.GetBehavior<BEBehaviorElectricalConverter>();
					powerconverted = converter.Powerconverted;
				}
			}
			base.FromTreeAttributes(tree,worldAccessForResolve);
		}
		public override void ToTreeAttributes(ITreeAttribute tree)
		{
			MPGeneratorBlockEntity mpgenerator = this.Blockentity as MPGeneratorBlockEntity;
			if(mpgenerator != null)
			{
				if(mpgenerator.GetBehavior<BEBehaviorElectricalConverter>() != null)
				{
					BEBehaviorElectricalConverter converter = mpgenerator.GetBehavior<BEBehaviorElectricalConverter>();
					converter.Powerconverted = Math.Max(0f,TrueSpeed*GetResistance());
				}
			}
			base.ToTreeAttributes(tree);
		}
		protected readonly BlockFacing[] orients = new BlockFacing[2];
		protected readonly BlockPos[] sides = new BlockPos[2];
		private ICoreClientAPI capi;
		private string orientations;
	}
}