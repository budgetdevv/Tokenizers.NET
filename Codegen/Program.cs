using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using Tokenizers.NET;
using Tokenizers.NET.Collections;

namespace Codegen
{
    internal static unsafe class Program
    {
        private struct TokenizerConfig: Tokenizer.IConfig
        {
            private static readonly Tokenizer.BuiltConfig BUILT_CONFIG =
                new Tokenizer.ConfigBuilder()
                    .SetExpectedMaxInputLength(512)
                    .SetExpectedMaxBatches(16)
                    .SetExceedExpectedMaxBatchesBehavior(Tokenizer.ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                    // .SetTokenizerJsonPath("FlorenceTokenizer.json")
                    .SetRawTokenizerData(new HttpClient().GetByteArrayAsync("https://raw.githubusercontent.com/budgetdevv/Tokenizers.NET/refs/heads/main/SampleTokenizers/FlorenceTokenizer.json").Result)
                    .Build();

            public static Tokenizer.BuiltConfig BuiltConfig => BUILT_CONFIG;
        }
        
        static void Main(string[] args)
        {
            // Ensure we ain't cheating by passing a constant span value
            // E.x. TokenizeBatch_DISASM(model.Tokenizer, [ "Hi", "Bye" ]);
            var inputs = new List<string>()
            {
                "Organic skincare for sensitive skin with aloe vera and chamomile.",
                "New makeup trends focus on bold colors and innovative techniques",
            }.ToArray();
            
            var tokenizer = new Tokenizer<TokenizerConfig>();
            
            var outputs = new NativeMemory<TokenizeOutput>((nuint) inputs.Length);
            
            for (int outerIterations = 0; outerIterations < 3; outerIterations++)
            {
                for (int innerIterations = 0; innerIterations < 1000; innerIterations++)
                {
                    Disasm(tokenizer, inputs, outputs);
                }
            
                Thread.Sleep(500);
            }
        }

        private static void Disasm(Tokenizer<TokenizerConfig> tokenizer, ReadOnlySpan<string> inputs, NativeMemory<TokenizeOutput> outputs)
        {
            TokenizeBatch_DISASM(tokenizer, inputs, outputs);
            
            DisposeTokenizeBatchOutput_DISASM(*outputs.Buffer.Ptr);
        }
        
        private const MethodImplOptions DISASM_METHOD_IMPL_OPTIONS = MethodImplOptions.NoInlining; // | MethodImplOptions.AggressiveOptimization;
        
        [MethodImpl(DISASM_METHOD_IMPL_OPTIONS)]
        private static void TokenizeBatch_DISASM(
            Tokenizer<TokenizerConfig> tokenizer,
            ReadOnlySpan<string> inputs,
            NativeMemory<TokenizeOutput> outputs)
        {
            tokenizer.TokenizeBatchInternal(
                inputs,
                outputs.Buffer,
                skipLengthCheck: true,
                addSpecialTokens: true
            );
        }
        
        [MethodImpl(DISASM_METHOD_IMPL_OPTIONS)]
        private static void DisposeTokenizeBatchOutput_DISASM(TokenizeOutput output)
        {
            output.Dispose();
        }
    }
}