using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Host;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Statement execution logic.
/// </summary>
internal sealed class StatementExecutor
{
    private readonly Interpreter _interpreter;
    private readonly ExecutionContext _ctx;
    private readonly IMathProvider _math;
    private readonly ITimeProvider _time;
    private readonly IRandomProvider _random;
    private readonly IUtf8Provider _utf8;
    private readonly StringMap<Lang.FunctionDecl> _functions;
    private readonly StringMap<HeapClass> _classes;
    private readonly StringMap<Lang.EnumDecl> _enums;
    private readonly UsingList _usings;
    private readonly StringMap<NativeFunction> _extensions;
    private readonly StringMap<int> _enumTypeIndices;
    private readonly StringList _enumNames;

    public StatementExecutor(Interpreter interpreter)
    {
        _interpreter = interpreter;
        _ctx = interpreter._ctx;
        _math = interpreter._math;
        _time = interpreter._time;
        _random = interpreter._random;
        _utf8 = interpreter._utf8;
        _functions = interpreter._functions;
        _classes = interpreter._classes;
        _enums = interpreter._enums;
        _usings = interpreter._usings;
        _extensions = interpreter._extensions;
        _enumTypeIndices = interpreter._enumTypeIndices;
        _enumNames = interpreter._enumNames;
    }

    public void Execute(Lang.Stmt stmt)
    {
        if (_ctx.CurrentFlow != ControlFlow.Normal) return;

        switch (stmt)
        {
            case Lang.VarDeclStmt v:
                _ctx.CurrentFrame.Locals[v.Name] = Value.NullValue;
                break;

            case Lang.AssignStmt a:
                var value = _interpreter.Evaluate(a.Value);
                _interpreter.AssignToTarget(a.Target, value);
                break;

            case Lang.PrintStmt p:
                IOStatements.ExecutePrint(_interpreter, p, _ctx);
                break;

            case Lang.ShowStmt s:
                IOStatements.ExecuteShow(_interpreter, s, _ctx);
                break;

            case Lang.InputStmt i:
                IOStatements.ExecuteInput(i, _ctx);
                break;

            case Lang.IfStmt iff:
                ExecuteIf(iff);
                break;

            case Lang.ForStmt f:
                ExecuteFor(f);
                break;

            case Lang.WhileStmt w:
                ExecuteWhile(w);
                break;

            case Lang.DoWhileStmt dw:
                ExecuteDoWhile(dw);
                break;

            case Lang.BreakStmt:
                if (_ctx.CurrentFlow == ControlFlow.Normal)
                    _ctx.CurrentFlow = ControlFlow.Break;
                break;

            case Lang.ContinueStmt:
                if (_ctx.CurrentFlow == ControlFlow.Normal)
                    _ctx.CurrentFlow = ControlFlow.Continue;
                break;

            case Lang.ReturnStmt r:
                _ctx.ReturnValue = r.Value != null ? _interpreter.Evaluate(r.Value) : Value.NullValue;
                if (_ctx.CurrentFlow == ControlFlow.Normal)
                    _ctx.CurrentFlow = ControlFlow.Return;
                break;

            case Lang.TryCatchStmt t:
                ExecuteTryCatch(t);
                break;

            case Lang.SwitchStmt sw:
                ExecuteSwitch(sw);
                break;

            case Lang.ClearStmt:
                _ctx.Host.Clear();
                break;

            case Lang.BaseCallStmt b:
                ExecuteBaseCall(b);
                break;

            case Lang.GlobalDeclStmt g:
                var gval = _interpreter.Evaluate(g.Value);
                while (_ctx.Globals.Count <= _usings.Count) _ctx.Globals.Add(Value.NullValue);
                _ctx.Globals.Add(gval);
                break;

            case Lang.ExpressionStmt e:
                _interpreter.Evaluate(e.Expression);
                break;

            case Lang.BlockStmt b:
                foreach (var s in b.Statements)
                {
                    Execute(s);
                    if (_ctx.CurrentFlow != ControlFlow.Normal) break;
                }
                break;
        }
    }

    private void ExecuteIf(Lang.IfStmt stmt)
    {
        var cond = _interpreter.Evaluate(stmt.Condition);
        if (Interpreter.IsTruthy(cond))
        {
            foreach (var s in stmt.ThenBody)
            {
                Execute(s);
                if (_ctx.CurrentFlow != ControlFlow.Normal) return;
            }
        }
        else
        {
            bool matched = false;
            foreach (var elif in stmt.ElifClauses)
            {
                var elifCond = _interpreter.Evaluate(elif.Condition);
                if (Interpreter.IsTruthy(elifCond))
                {
                    foreach (var s in elif.Body)
                    {
                        Execute(s);
                        if (_ctx.CurrentFlow != ControlFlow.Normal) return;
                    }
                    matched = true;
                    break;
                }
            }

            if (!matched && stmt.ElseBody != null)
            {
                foreach (var s in stmt.ElseBody)
                {
                    Execute(s);
                    if (_ctx.CurrentFlow != ControlFlow.Normal) return;
                }
            }
        }
    }

    private void ExecuteFor(Lang.ForStmt stmt)
    {
        var fromVal = _interpreter.Evaluate(stmt.From);
        var toVal = _interpreter.Evaluate(stmt.To);
        var stepVal = stmt.Step != null ? _interpreter.Evaluate(stmt.Step) : Value.Number(1);

        if (fromVal.Kind != ValueKind.Number || toVal.Kind != ValueKind.Number || stepVal.Kind != ValueKind.Number)
        {
            _ctx.Throw("FOR loop bounds must be numbers", stmt.Location);
            return;
        }

        double from = fromVal.AsNumber;
        double to = toVal.AsNumber;
        double step = stepVal.AsNumber;

        if (step == 0)
        {
            _ctx.Throw("FOR loop STEP cannot be zero", stmt.Location);
            return;
        }

        _ctx.CurrentFrame.Locals[stmt.Variable] = Value.Number(from);
        _ctx.LoopDepth++;

        while (true)
        {
            var current = _ctx.CurrentFrame.Locals[stmt.Variable].AsNumber;
            bool continueLoop = step > 0 ? current <= to : current >= to;

            if (!continueLoop) break;

            foreach (var s in stmt.Body)
            {
                Execute(s);
                if (_ctx.CurrentFlow == ControlFlow.Break) { _ctx.CurrentFlow = ControlFlow.Normal; goto ExitLoop; }
                if (_ctx.CurrentFlow == ControlFlow.Continue) { _ctx.CurrentFlow = ControlFlow.Normal; break; }
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitLoop;
            }

            _ctx.CurrentFrame.Locals[stmt.Variable] = Value.Number(current + step);
        }

    ExitLoop:
        _ctx.LoopDepth--;
    }

    private void ExecuteWhile(Lang.WhileStmt stmt)
    {
        _ctx.LoopDepth++;
        while (true)
        {
            var cond = _interpreter.Evaluate(stmt.Condition);
            if (!Interpreter.IsTruthy(cond)) break;

            foreach (var s in stmt.Body)
            {
                Execute(s);
                if (_ctx.CurrentFlow == ControlFlow.Break) { _ctx.CurrentFlow = ControlFlow.Normal; goto ExitLoop; }
                if (_ctx.CurrentFlow == ControlFlow.Continue) { _ctx.CurrentFlow = ControlFlow.Normal; break; }
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitLoop;
            }
        }
    ExitLoop:
        _ctx.LoopDepth--;
    }

    private void ExecuteDoWhile(Lang.DoWhileStmt stmt)
    {
        _ctx.LoopDepth++;
        while (true)
        {
            foreach (var s in stmt.Body)
            {
                Execute(s);
                if (_ctx.CurrentFlow == ControlFlow.Break) { _ctx.CurrentFlow = ControlFlow.Normal; goto ExitLoop; }
                if (_ctx.CurrentFlow == ControlFlow.Continue) { _ctx.CurrentFlow = ControlFlow.Normal; break; }
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitLoop;
            }

            var cond = _interpreter.Evaluate(stmt.Condition);
            if (!Interpreter.IsTruthy(cond)) break;
        }
    ExitLoop:
        _ctx.LoopDepth--;
    }

    private void ExecuteTryCatch(Lang.TryCatchStmt stmt)
    {
        int prevTryFrame = _ctx.CurrentFrame.TryCatchFrameIndex;
        _ctx.CurrentFrame.TryCatchFrameIndex = _ctx.Frames.Count - 1;
        _ctx.CurrentFrame.InTry = true;

        foreach (var s in stmt.TryBody)
        {
            Execute(s);
            if (_ctx.CurrentFlow != ControlFlow.Normal) break;
        }

        if (_ctx.CurrentFlow == ControlFlow.Exception)
        {
            _ctx.CurrentFrame.Locals[stmt.ErrorVariable] = _ctx.CurrentException;
            _ctx.CurrentFlow = ControlFlow.Normal;
            _ctx.CurrentException = Value.NullValue;

            foreach (var s in stmt.CatchBody)
            {
                Execute(s);
                if (_ctx.CurrentFlow != ControlFlow.Normal) break;
            }
        }

        _ctx.CurrentFrame.TryCatchFrameIndex = prevTryFrame;
        _ctx.CurrentFrame.InTry = false;
    }

    private void ExecuteSwitch(Lang.SwitchStmt stmt)
    {
        var value = _interpreter.Evaluate(stmt.Value);
        bool matched = false;
        _ctx.InSwitch = true;

        foreach (var case_ in stmt.Cases)
        {
            foreach (var caseVal in case_.Values)
            {
                var cv = _interpreter.Evaluate(caseVal);
                if (ValueUtilities.ValuesEqual(value, cv))
                {
                    foreach (var s in case_.Body)
                    {
                        Execute(s);
                        if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitSwitch;
                    }
                    matched = true;
                    goto ExitSwitch;
                }
            }
        }

        if (!matched && stmt.DefaultBody != null)
        {
            foreach (var s in stmt.DefaultBody)
            {
                Execute(s);
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitSwitch;
            }
        }

    ExitSwitch:
        _ctx.InSwitch = false;
    }

    private void ExecuteBaseCall(Lang.BaseCallStmt stmt)
    {
        var frame = _ctx.CurrentFrame;
        if (frame.Base.IsNull)
        {
            _ctx.Throw("BASE() called outside constructor", stmt.Location);
            return;
        }

        foreach (var arg in stmt.Args)
        {
            _interpreter.Evaluate(arg);
        }
    }
}