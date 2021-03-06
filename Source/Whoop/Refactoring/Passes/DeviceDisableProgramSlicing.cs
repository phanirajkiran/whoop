﻿// ===-----------------------------------------------------------------------==//
//
//                 Whoop - a Verifier for Device Drivers
//
//  Copyright (c) 2013-2014 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
//
//  This file is distributed under the Microsoft Public License.  See
//  LICENSE.TXT for details.
//
// ===----------------------------------------------------------------------===//

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

using Microsoft.Boogie;
using Microsoft.Basetypes;
using Whoop.Analysis;
using Whoop.Domain.Drivers;
using Whoop.Regions;

namespace Whoop.Refactoring
{
  internal class DeviceDisableProgramSlicing : ProgramSlicing, IPass
  {
    #region public API

    private ExecutionTimer Timer;

    #endregion

    #region public API

    public DeviceDisableProgramSlicing(AnalysisContext ac, EntryPoint ep)
      : base(ac, ep)
    {
      base.ChangingRegion = base.AC.InstrumentationRegions.Find(val => val.IsChangingDeviceRegistration);
    }

    public void Run()
    {
      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer = new ExecutionTimer();
        this.Timer.Start();
      }

      foreach (var region in base.AC.InstrumentationRegions)
      {
        this.SimplifyCallsInRegion(region);
      }

      foreach (var region in base.SlicedRegions)
      {
        base.SliceRegion(region);
      }

      this.SimplifyChangingRegion();
      var predecessors = base.EP.CallGraph.NestedPredecessors(base.ChangingRegion);
      var successors = base.EP.CallGraph.NestedSuccessors(base.ChangingRegion);
      predecessors.RemoveWhere(val => successors.Contains(val));
      this.SimplifyPredecessors(predecessors);

      foreach (var region in base.AC.InstrumentationRegions)
      {
        ReadWriteSlicing.CleanReadWriteModsets(base.AC, base.EP, region);
      }

      if (WhoopCommandLineOptions.Get().MeasurePassExecutionTime)
      {
        this.Timer.Stop();
        Console.WriteLine(" |  |------ [DeviceDisableProgramSlicing] {0}", this.Timer.Result());
      }
    }

    #endregion

    #region device program slicing functions

    private void SimplifyCallsInRegion(InstrumentationRegion region)
    {
      foreach (var call in region.Cmds().OfType<CallCmd>())
      {
        var calleeRegion = base.AC.InstrumentationRegions.Find(val =>
          val.Implementation().Name.Equals(call.callee));
        if (calleeRegion == null)
          continue;
        if (calleeRegion.IsDeviceRegistered || calleeRegion.IsChangingDeviceRegistration)
          continue;

        call.callee = "_NO_OP_$" + base.EP.Name;
        call.Ins.Clear();
        call.Outs.Clear();

        base.SlicedRegions.Add(calleeRegion);
      }
    }

    private void SimplifyChangingRegion()
    {
      var blockGraph = Graph<Block>.BuildBlockGraph(base.ChangingRegion.Blocks());

      Block devBlock = null;
      CallCmd devCall = null;

      foreach (var block in base.ChangingRegion.Blocks())
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (devBlock == null && call.callee.StartsWith("_UNREGISTER_DEVICE_"))
          {
            devBlock = block;
            devCall = call;
            break;
          }
        }

        if (devBlock != null)
          break;
      }

      this.SimplifyBlocks(base.ChangingRegion, blockGraph, devBlock, devCall);
    }

    private void SimplifyPredecessors(HashSet<InstrumentationRegion> predecessors)
    {
      foreach (var region in predecessors)
      {
        var blockGraph = Graph<Block>.BuildBlockGraph(region.Blocks());

        Block devBlock = null;
        CallCmd devCall = null;

        foreach (var block in region.Blocks())
        {
          foreach (var call in block.Cmds.OfType<CallCmd>())
          {
            if (devBlock == null && (predecessors.Any(val =>
              val.Implementation().Name.Equals(call.callee)) ||
              base.ChangingRegion.Implementation().Name.Equals(call.callee)))
            {
              devBlock = block;
              devCall = call;
              break;
            }
          }

          if (devBlock != null)
            break;
        }

        this.SimplifyBlocks(region, blockGraph, devBlock, devCall);
      }
    }

    private void SimplifyBlocks(InstrumentationRegion region, Graph<Block> blockGraph,
      Block devBlock, CallCmd devCall)
    {
      var predecessorBlocks = blockGraph.NestedPredecessors(devBlock);
      var successorBlocks = blockGraph.NestedSuccessors(devBlock);
      successorBlocks.RemoveWhere(val =>
        predecessorBlocks.Contains(val) || val.Equals(devBlock));

      foreach (var block in successorBlocks)
      {
        foreach (var call in block.Cmds.OfType<CallCmd>())
        {
          if (call.callee.StartsWith("_WRITE_LS_$M.") ||
            call.callee.StartsWith("_READ_LS_$M."))
          {
            ReadWriteSlicing.CleanReadWriteSets(base.EP, region, call);

            call.callee = "_NO_OP_$" + base.EP.Name;
            call.Ins.Clear();
            call.Outs.Clear();
          }
          else
          {
            var calleeRegion = base.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            if (calleeRegion == null)
              continue;

            call.callee = "_NO_OP_$" + base.EP.Name;
            call.Ins.Clear();
            call.Outs.Clear();
          }
        }
      }

      if (!predecessorBlocks.Contains(devBlock))
      {
        bool foundCall = false;
        foreach (var call in devBlock.Cmds.OfType<CallCmd>())
        {
          if (!foundCall && call.Equals(devCall))
          {
            foundCall = true;
            continue;
          }

          if (!foundCall)
            continue;

          if (call.callee.StartsWith("_WRITE_LS_$M.") ||
            call.callee.StartsWith("_READ_LS_$M."))
          {
            ReadWriteSlicing.CleanReadWriteSets(base.EP, region, call);

            call.callee = "_NO_OP_$" + base.EP.Name;
            call.Ins.Clear();
            call.Outs.Clear();
          }
          else
          {
            var calleeRegion = base.AC.InstrumentationRegions.Find(val =>
              val.Implementation().Name.Equals(call.callee));
            if (calleeRegion == null)
              continue;

            call.callee = "_NO_OP_$" + base.EP.Name;
            call.Ins.Clear();
            call.Outs.Clear();
          }
        }
      }
    }

    #endregion
  }
}
