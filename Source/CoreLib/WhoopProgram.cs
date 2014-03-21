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
  public class WhoopProgram : CheckingContext
  {
    public Program program;
    public ResolutionContext resContext;

    public Dictionary<string, Dictionary<string, string>> entryPoints;
    public Implementation mainFunc;
    public List<Variable> memoryRegions;

    public Lockset currLockset;
    public List<Lockset> locksets;

    public Microsoft.Boogie.Type memoryModelType;

    internal SharedStateAnalyser sharedStateAnalyser;

    public WhoopProgram(Program program, ResolutionContext rc)
      : base((IErrorSink)null)
    {
      Contract.Requires(program != null);
      Contract.Requires(rc != null);

      this.program = program;
      this.resContext = rc;
      this.locksets = new List<Lockset>();

      if (Util.GetCommandLineOptions().MemoryModel.Equals("default")) {
        this.memoryModelType = Microsoft.Boogie.Type.Int;
      }

      this.entryPoints = IO.ParseDriverInfo();
      this.sharedStateAnalyser = new SharedStateAnalyser(this);

      DetectMainFunction();

      this.memoryRegions = sharedStateAnalyser.GetMemoryRegions();
    }

    public void EliminateDeadVariables()
    {
      ExecutionEngine.EliminateDeadVariables(program);
    }

    public void Inline()
    {
      ExecutionEngine.Inline(program);
    }

    public List<Implementation> GetImplementationsToAnalyse()
    {
      return program.TopLevelDeclarations.OfType<Implementation>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "entry_pair"));
    }

    public List<Variable> GetRaceCheckingVariables()
    {
      return program.TopLevelDeclarations.OfType<Variable>().ToList().
        FindAll(val => QKeyValue.FindBoolAttribute(val.Attributes, "access_checking"));
    }

    public Implementation GetImplementation(string name)
    {
      Contract.Requires(name != null);
      Implementation impl = (program.TopLevelDeclarations.Find(val => (val is Implementation) &&
                            (val as Implementation).Name.Equals(name)) as Implementation);
      return impl;
    }

    public Constant GetConstant(string name)
    {
      Contract.Requires(name != null);
      Constant cons = (program.TopLevelDeclarations.Find(val => (val is Constant) &&
                      (val as Constant).Name.Equals(name)) as Constant);
      return cons;
    }

    public bool isWhoopFunc(string name)
    {
      if (name.Contains("_UPDATE_CURRENT_LOCKSET") ||
          name.Contains("_LOG_WRITE_LS_") || name.Contains("_LOG_READ_LS_") ||
          name.Contains("_CHECK_WRITE_LS_") || name.Contains("_CHECK_READ_LS_") ||
          name.Contains("_CHECK_ALL_LOCKS_HAVE_BEEN_RELEASED"))
        return true;
      return false;
    }

    public bool isCalledByAnEntryPoint(string name)
    {
      foreach (var ep in GetImplementationsToAnalyse()) {
        foreach (var b in ep.Blocks) {
          foreach (var c in b.Cmds.OfType<CallCmd>()) {
            if (c.callee.Equals(name)) return true;
          }
        }
      }
      return false;
    }

    internal Function GetOrCreateBVFunction(string functionName, string smtName, Microsoft.Boogie.Type resultType)
    {
      Function f = (Function) resContext.LookUpProcedure(functionName);
      if (f != null)
        return f;

      f = new Function(Token.NoToken, functionName,
        new List<Variable>(new LocalVariable[] {
          new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "", resultType)),
          new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "", resultType))
        }), new LocalVariable(Token.NoToken, new TypedIdent(Token.NoToken, "", resultType)));
      f.AddAttribute("bvbuiltin", smtName);

      program.TopLevelDeclarations.Add(f);
      resContext.AddProcedure(f);

      return f;
    }

    internal Expr MakeBVFunctionCall(string functionName, string smtName, Microsoft.Boogie.Type resultType, params Expr[] args)
    {
      Function f = GetOrCreateBVFunction(functionName, smtName, resultType);
      var e = new NAryExpr(Token.NoToken, new FunctionCall(f), new List<Expr>(args));
      return e;
    }

    private void DetectMainFunction()
    {
      string mainFuncName = null;
      bool found = false;

      try {
        foreach (var kvp in entryPoints) {
          foreach (var ep in kvp.Value) {
            if (ep.Key.Equals("probe")) {
              mainFuncName = ep.Value;
              found = true;
              break;
            }
          }
          if (found) break;
        }
        if (!found) throw new Exception("no main function found");
        mainFunc = (program.TopLevelDeclarations.Find(val => (val is Implementation) &&
          (val as Implementation).Name.Equals(mainFuncName)) as Implementation);
        if (mainFunc == null) throw new Exception("no main function found");
      } catch (Exception e) {
        Console.Error.Write("Exception thrown in Whoop: ");
        Console.Error.WriteLine(e);
      }
    }
  }
}
  