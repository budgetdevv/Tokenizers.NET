﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tokenizers.NET;
using Tokenizers.NET.Outputs;
using NativeMemory;

namespace Codegen
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            // Ensure we ain't cheating by passing a constant span value
            // E.x. TokenizeBatch_DISASM(model.Tokenizer, [ "Hi", "Bye" ]);
            var inputs = new List<string>()
            {
                "Organic skincare for sensitive skin with aloe vera and chamomile.",
                "New makeup trends focus on bold colors and innovative techniques",
            }.ToArray();

            var tokenizer = (await new TokenizerBuilder()
                .SetExpectedMaxInputLength(512)
                .SetExpectedMaxBatches(16)
                .SetExceedExpectedMaxBatchesBehavior(ExceedExpectedMaxBatchesBehavior.AllocateBuffer)
                .DownloadFromHuggingFaceRepoAsync("microsoft/Florence-2-large"))
                .Build();
            
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

        private static unsafe void Disasm(
            Tokenizer tokenizer,
            ReadOnlySpan<string> inputs,
            NativeMemory<TokenizeOutput> outputs)
        {
            Tokenize_DISASM(tokenizer, inputs[0]);
            
            TokenizeBatch_DISASM(tokenizer, inputs, outputs);
            
            DisposeTokenizeBatchOutput_DISASM(*outputs.Window.Ptr);
        }
        
        private const MethodImplOptions DISASM_METHOD_IMPL_OPTIONS = MethodImplOptions.NoInlining; // | MethodImplOptions.AggressiveOptimization;

        [MethodImpl(DISASM_METHOD_IMPL_OPTIONS)]
        private static void Tokenize_DISASM(Tokenizer tokenizer, string input)
        {
            tokenizer.TokenizeInternal(
                input,
                addSpecialTokens: true
            );
        }
        
        [MethodImpl(DISASM_METHOD_IMPL_OPTIONS)]
        private static void TokenizeBatch_DISASM(
            Tokenizer tokenizer,
            ReadOnlySpan<string> inputs,
            NativeMemory<TokenizeOutput> outputs)
        {
            tokenizer.TokenizeBatchInternal(
                inputs,
                outputs.Window,
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