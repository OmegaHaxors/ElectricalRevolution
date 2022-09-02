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
}