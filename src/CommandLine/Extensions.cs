﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Roslynator.CommandLine
{
    internal static class Extensions
    {
        public static OperationCanceledException GetOperationCanceledException(this AggregateException aggregateException)
        {
            OperationCanceledException operationCanceledException = null;

            foreach (Exception ex in aggregateException.InnerExceptions)
            {
                if (ex is OperationCanceledException operationCanceledException2)
                {
                    if (operationCanceledException == null)
                        operationCanceledException = operationCanceledException2;
                }
                else if (ex is AggregateException aggregateException2)
                {
                    foreach (Exception ex2 in aggregateException2.InnerExceptions)
                    {
                        if (ex2 is OperationCanceledException operationCanceledException3)
                        {
                            if (operationCanceledException == null)
                                operationCanceledException = operationCanceledException3;
                        }
                        else
                        {
                            return null;
                        }
                    }

                    return operationCanceledException;
                }
                else
                {
                    return null;
                }
            }

            return null;
        }
    }
}