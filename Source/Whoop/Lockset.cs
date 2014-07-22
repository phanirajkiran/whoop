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
using System.Diagnostics.Contracts;
using Microsoft.Boogie;

namespace Whoop
{
  internal class Lockset
  {
    public readonly Variable Id;
    public readonly Variable Lock;
    public string TargetName;

    public Lockset(Variable id, Variable l, string target = "")
    {
      this.Id = id;
      this.Lock = l;
      this.TargetName = target;
    }
  }
}