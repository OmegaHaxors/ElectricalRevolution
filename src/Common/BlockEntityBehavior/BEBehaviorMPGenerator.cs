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
			float theresistance = this.TrueSpeed;
			return theresistance;
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
					powerconverted = converter.Powerconverted * 100000;
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
					converter.Powerconverted = Math.Max(0f,TrueSpeed*GetResistance())*100000;
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