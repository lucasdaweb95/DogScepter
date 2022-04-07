﻿using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains information about a GameMaker function.
    /// </summary>
    public class GMFunctionEntry : IGMSerializable
    {
        public GMString Name;
        public int StringIndex; // Index of the string in the STRG chunk, appears to be unused(?)
        public int Occurrences;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write(Occurrences);

            List<int> references;
            if (writer.FunctionReferences.TryGetValue(this, out references))
                Occurrences = references.Count;
            else
                Occurrences = 0;

            if (Occurrences > 0)
            {
                if (writer.VersionInfo.IsNumberAtLeast(2, 3))
                    writer.Write(references[0] + 4);
                else
                    writer.Write(references[0]);

                int returnTo = writer.Offset;
                for (int i = 0; i < references.Count; i++)
                {
                    int curr = references[i];

                    int nextDiff;
                    if (i < references.Count - 1)
                        nextDiff = references[i + 1] - curr;
                    else
                        nextDiff = ((GMChunkSTRG)writer.Data.Chunks["STRG"]).List.IndexOf(Name);

                    writer.Offset = curr + 4;
                    writer.WriteInt24(nextDiff);
                }
                writer.Offset = returnTo;
            }
            else
                writer.Write((int)-1);
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Occurrences = reader.ReadInt32();
            if (Occurrences > 0)
            {
                int addr;
                if (reader.VersionInfo.IsNumberAtLeast(2, 3))
                    addr = reader.ReadInt32() - 4;
                else
                    addr = reader.ReadInt32();

                // Parse reference chain
                GMCode.Bytecode.Instruction curr;
                for (int i = Occurrences; i > 0; i--)
                {
                    curr = reader.Instructions[addr];
                    if (curr.Function == null)
                    {
                        curr.Function = new GMCode.Bytecode.Instruction.Reference<GMFunctionEntry>((int)curr.Value);
                        curr.Value = null;
                    }
                    curr.Function.Target = this;
                    addr += curr.Function.NextOccurrence;
                }
            }
            else
            {
                if (reader.ReadInt32() != -1)
                    reader.Warnings.Add(new GMWarning("Function with no occurrences, but still has a first occurrence address"));
            }
        }

        public override string ToString()
        {
            return $"Function: \"{Name.Content}\"";
        }

    }
}
