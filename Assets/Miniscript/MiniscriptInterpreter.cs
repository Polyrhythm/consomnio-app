using System.Diagnostics;
using System.Collections.Generic;

namespace Miniscript {

	public delegate void TextOutputMethod(string output);

	public class Interpreter {
		
		public TextOutputMethod standardOutput {
			get {
				return _standardOutput;
			}
			set {
				_standardOutput = value;
				if (vm != null) vm.standardOutput = value;
			}
		}

		public TextOutputMethod implicitOutput;
		public TextOutputMethod errorOutput;

		public object hostData;		// extra data for use by the host app

		TextOutputMethod _standardOutput;
		string source;
		Parser parser;
		TAC.Machine vm;

		public Interpreter(string source=null, TextOutputMethod standardOutput=null, TextOutputMethod errorOutput=null) {
			this.source = source;
			if (standardOutput == null) standardOutput = delegate (string text) { Debug.WriteLine(text); };
            if (errorOutput == null) errorOutput = delegate (string text) { Debug.WriteLine(text); };
			this.standardOutput = standardOutput;
			this.errorOutput = errorOutput;
		}

		public Interpreter(List<string> source) : this(string.Join("\n", source.ToArray())) {
		}

		public Interpreter(string[] source) : this(string.Join("\n", source)) {
		}
		
		/// <summary>
		/// Stop the virtual machine, and jump to the end of the program code.
		/// </summary>
		public void Stop() {
			if (vm != null) vm.Stop();
		}
		
		/// <summary>
		/// Reset the interpreter with the given source code.
		/// </summary>
		/// <param name="source"></param>
		public void Reset(string source="") {
			this.source = source;
			parser = null;
			vm = null;
		}
		
		/// <summary>
		/// Compile our source code, if we haven't already done so, so that we are
		/// either ready to run, or generate compiler errors (reported via errorOutput).
		/// </summary>
		public void Compile() {
			if (vm != null) return;	// already compiled

			if (parser == null) parser = new Parser();
			try {
				parser.Parse(source);
				vm = parser.CreateVM(standardOutput);
				vm.interpreter = new System.WeakReference(this);
			} catch (MiniscriptException mse) {
				ReportError(mse);
			}
		}
		
		/// <summary>
		/// Reset the virtual machine to the beginning of the code.  Note that this
		/// does *not* reset global variables; it simply clears the stack and jumps
		/// to the beginning.  Useful in cases where you have a short script you
		/// want to run over and over, without recompiling every time.
		/// </summary>
		public void Restart() {
			if (vm != null) vm.Reset();			
		}
		
		/// <summary>
		/// Run the compiled code until we either reach the end, or we reach the
		/// specified time limit.  In the latter case, you can then call RunUntilDone
		/// again to continue execution right from where it left off.
		/// 
		/// Note that this method first compiles the source code if it wasn't compiled
		/// already, and in that case, may generate compiler errors.  And of course
		/// it may generate runtime errors while running.  In either case, these are
		/// reported via errorOutput.
		/// </summary>
		/// <param name="timeLimit">maximum amout of time to run before returning, in seconds</param>
		public void RunUntilDone(double timeLimit=60) {
			try {
				if (vm == null) {
					Compile();
					if (vm == null) return;	// (must have been some error)
				}
				double startTime = vm.runTime;
				while (!vm.done) {
					if (vm.runTime - startTime > timeLimit) return;	// time's up for now!
					vm.Step();
				}
			} catch (MiniscriptException mse) {
				ReportError(mse);
			}
		}
		
		/// <summary>
		/// Run one step of the virtual machine.  This method is not very useful
		/// except in special cases; usually you will use RunUntilDone (above) instead.
		/// </summary>
		public void Step() {
			try {
				Compile();
				vm.Step();
			} catch (MiniscriptException mse) {
				ReportError(mse);
			}
		}

		/// <summary>
		/// Read Eval Print Loop.  Run the given source until it either terminates,
		/// or hits the given time limit.  When it terminates, if we have new
		/// implicit output, print that to the implicitOutput stream.
		/// </summary>
		/// <param name="sourceLine">Source line.</param>
		/// <param name="timeLimit">Time limit.</param>
		public void REPL(string sourceLine, double timeLimit=60) {
			if (parser == null) parser = new Parser();
			if (vm == null) {
				vm = parser.CreateVM(standardOutput);
				vm.interpreter = new System.WeakReference(this);
			}
			if (sourceLine == "#DUMP") {
				vm.DumpTopContext();
				return;
			}
			
			double startTime = vm.runTime;
			int startImpResultCount = vm.globalContext.implicitResultCounter;
			vm.storeImplicit = (implicitOutput != null);

			try {
				if (sourceLine != null) parser.Parse(sourceLine, true);
				if (!parser.NeedMoreInput()) {
					while (!vm.done) {
						if (vm.runTime - startTime > timeLimit) return;	// time's up for now!
						vm.Step();
					}
					if (implicitOutput != null && vm.globalContext.implicitResultCounter > startImpResultCount) {
						Value result = vm.globalContext.GetVar(ValVar.implicitResult.identifier);
						if (result != null) implicitOutput.Invoke(result.ToString());
					}
				}

			} catch (MiniscriptException mse) {
				ReportError(mse);
				// Attempt to recover from an error by jumping to the end of the code.
				vm.GetTopContext().JumpToEnd();
			}
		}
		
		/// <summary>
		/// Report whether the virtual machine is still running, that is,
		/// whether it has not yet reached the end of the program code.
		/// </summary>
		/// <returns></returns>
		public bool Running() {
			return vm != null && !vm.done;
		}
		
		/// <summary>
		/// Return whether the parser needs more input, for example because we have
		/// run out of source code in the middle of an "if" block.  This is typically
		/// used with REPL for making an interactive console, so you can change the
		/// prompt when more input is expected.
		/// </summary>
		/// <returns></returns>
		public bool NeedMoreInput() {
			return parser != null && parser.NeedMoreInput();
		}
		
		/// <summary>
		/// Get a value from the global namespace of this interpreter.
		/// </summary>
		/// <param name="varName">name of global variable to get</param>
		/// <returns>Value of the named variable, or null if not found</returns>
		public Value GetGlobalValue(string varName) {
			if (vm == null) return null;
			TAC.Context c = vm.globalContext;
			if (c == null) return null;
			return c.GetVar(varName);
		}
		
		/// <summary>
		/// Set a value in the global namespace of this interpreter.
		/// </summary>
		/// <param name="varName">name of global variable to set</param>
		/// <param name="value">value to set</param>
		public void SetGlobalValue(string varName, Value value) {
			if (vm != null) vm.globalContext.SetVar(varName, value);
		}
		
		protected virtual void ReportError(MiniscriptException mse) {
			errorOutput.Invoke(mse.Description());
		}
	}
}
