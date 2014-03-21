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

namespace whoop
{
  public class PairWiseLocksetInstrumentation : LocksetInstrumentation
  {
    public PairWiseLocksetInstrumentation(WhoopProgram wp)
      : base(wp)
    {

    }

    public void Run()
    {
      AddCurrentLockset();
      AddMemoryLocksets();
      AddUpdateLocksetFunc();

      InstrumentEntryPoints();
      InstrumentOtherFuncs();
    }
  }
}
