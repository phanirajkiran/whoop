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

namespace whoop
{
  public class MainFunctionInstrumentation
  {
    WhoopProgram wp;

    public MainFunctionInstrumentation(WhoopProgram wp)
    {
      this.wp = wp;
    }

    public void Run()
    {
      InstrumentProcedure();
      InstrumentImplementation();
      CleanUp();
    }

    private void InstrumentImplementation()
    {
      wp.mainFunc.Blocks[wp.mainFunc.Blocks.Count - 1].TransferCmd =
        new GotoCmd(Token.NoToken, new List<string>() { "$bb" + wp.mainFunc.Blocks.Count });

      Block b = new Block(Token.NoToken, "$bb" + wp.mainFunc.Blocks.Count,
                  new List<Cmd>(), new ReturnCmd(Token.NoToken));

      List<Variable> dummiesCLS = new List<Variable>();
      Variable dummyLock = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "lock",
                             Microsoft.Boogie.Type.Int));
      dummiesCLS.Add(dummyLock);

      b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
        new ForallExpr(Token.NoToken, dummiesCLS,
          Expr.Iff(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
            new List<Expr>(new Expr[] {
              new IdentifierExpr(wp.currLockset.id.tok, wp.currLockset.id),
              new IdentifierExpr(dummyLock.tok, dummyLock)
            })),
            Expr.False))));

      List<Variable> dummiesLS = new List<Variable>();
      Variable dummyPtr = new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "ptr",
                            Microsoft.Boogie.Type.Int));
      dummiesLS.Add(dummyPtr);
      dummiesLS.Add(dummyLock);

      foreach (var ls in wp.locksets) {
        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
          new ForallExpr(Token.NoToken, dummiesLS,
            Expr.Iff(new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
              new List<Expr>(new Expr[] {
                new NAryExpr(Token.NoToken, new MapSelect(Token.NoToken, 1),
                  new List<Expr>(new Expr[] {
                    new IdentifierExpr(ls.id.tok, ls.id),
                    new IdentifierExpr(dummyPtr.tok, dummyPtr)
                  })),
                new IdentifierExpr(dummyLock.tok, dummyLock)
              })), Expr.True))));
      }

      foreach (var v in wp.sharedStateAnalyser.GetMemoryRegions()) {
        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("_HAS_OCCURRED_") && val.Name.Contains(v.Name));

        b.Cmds.Insert(b.Cmds.Count, new AssumeCmd(Token.NoToken,
          Expr.Iff(new IdentifierExpr(raceCheck.tok, raceCheck), Expr.False)));
      }

      foreach (var impl in wp.GetImplementationsToAnalyse()) {
        string[] str = impl.Name.Split(new Char[] { '$' });
        Contract.Requires(str.Length == 3);

        CallCmd c1 = (wp.mainFunc.Blocks[0].Cmds.Find(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[1])) as CallCmd);
        CallCmd c2 = (wp.mainFunc.Blocks[0].Cmds.Find(val => (val is CallCmd) &&
          (val as CallCmd).callee.Equals(str[2])) as CallCmd);

        List<Expr> ins = new List<Expr>();
        foreach (var e in c1.Ins) ins.Add(e.Clone() as Expr);
        foreach (var e in c2.Ins) ins.Add(e.Clone() as Expr);

        b.Cmds.Add(new CallCmd(Token.NoToken, impl.Name, ins, new List<IdentifierExpr>()));
      }

      wp.mainFunc.Blocks.Add(b);
    }

    private void InstrumentProcedure()
    {
      wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, wp.currLockset.id));
      foreach (var ls in wp.locksets)
        wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(Token.NoToken, ls.id));
      wp.mainFunc.Blocks.Reverse();

      foreach (var v in wp.sharedStateAnalyser.GetMemoryRegions()) {
        Variable raceCheck = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("_HAS_OCCURRED_") && val.Name.Contains(v.Name));
        Variable offset = wp.GetRaceCheckingVariables().Find(val =>
          val.Name.Contains("ACCESS_OFFSET_") && val.Name.Contains(v.Name));

        if (!wp.mainFunc.Proc.Modifies.Exists(val => val.Name.Equals(raceCheck.Name))) {
          wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(raceCheck.tok, raceCheck));
          wp.mainFunc.Proc.Modifies.Add(new IdentifierExpr(offset.tok, offset));
        }
      }
    }

    private void CleanUp()
    {
      wp.mainFunc.Blocks[0].Cmds.RemoveAll(val1 => (val1 is CallCmd) && wp.GetImplementationsToAnalyse().Exists(val2 =>
        val2.Name.Contains((val1 as CallCmd).callee)));
    }
  }
}
