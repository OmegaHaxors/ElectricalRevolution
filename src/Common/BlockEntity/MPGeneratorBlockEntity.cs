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
	public class MPGeneratorBlockEntity : BlockEntity
	{
		BEBehaviorMPGenerator mpc = null;
		bool powered = false;

		public override void Initialize(ICoreAPI api)
		{
			base.Initialize(api);
			RegisterGameTickListener(UpdateNBT,1000);//only runs on the server (set to 100)
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
}