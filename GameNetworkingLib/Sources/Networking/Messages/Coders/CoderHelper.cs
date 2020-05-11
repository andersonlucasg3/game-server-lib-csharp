﻿using System;
using System.Collections.Generic;
using System.Text;
using GameNetworking.Messages.Coders.Converters;

namespace GameNetworking.Messages.Coders {
    internal static class CoderHelper {
        private static IntByteArrayConverter _intConverter = new IntByteArrayConverter(0);
        internal static byte[] delimiter = Encoding.UTF8.GetBytes("h;'y#@%tf$ˆbh");

        internal static int InsertDelimiter(byte[] buffer, int index) {
            Array.Copy(delimiter, 0, buffer, index, delimiter.Length);
            return delimiter.Length;
        }

        internal static int CheckForDelimiter(byte[] buffer, int length) {
            return ArraySearch.IndexOf(buffer, delimiter, length);
        }

        internal static void PackageBytes(int size, byte[] buffer, byte[] packetBytes) {
            Array.Copy(buffer, packetBytes, size);
        }

        internal static int SliceBuffer(int delimiterIndex, byte[] buffer, int count) {
            var delimiterEndIndex = delimiterIndex + delimiter.Length;
            var newLength = count - delimiterEndIndex;
            Array.Copy(buffer, delimiterEndIndex, buffer, 0, newLength);
            return newLength;
        }

        internal static int WriteHeader(int type, byte[] buffer, int index) {
            var headerSize = sizeof(int);
            _intConverter.value = type;
            Array.Copy(_intConverter.array, 0, buffer, index, headerSize);
            return headerSize;
        }
    }

    internal static class ArraySearch {
        private struct PartialMatch : IEquatable<PartialMatch> {
            public int Index { get; private set; }
            public int MatchLength { get; set; }

            public PartialMatch(int index) {
                Index = index;
                MatchLength = 1;
            }

            bool IEquatable<PartialMatch>.Equals(PartialMatch other) {
                return this.Index == other.Index && this.MatchLength == other.MatchLength;
            }
        }

        internal static int IndexOf(byte[] arrayToSearch, byte[] patternToFind, int length) {
            if (patternToFind.Length == 0 || length == 0 || length < patternToFind.Length) {
                return -1;
            }

            List<PartialMatch> partialMatches = new List<PartialMatch>();

            for (int i = 0; i < length; i++) {
                byte searching = arrayToSearch[i];

                for (int j = partialMatches.Count - 1; j >= 0; j--) {
                    var partMatch = partialMatches[j];

                    if (searching == patternToFind[partMatch.MatchLength]) {
                        partMatch.MatchLength++;

                        if (partMatch.MatchLength == patternToFind.Length) {
                            return partMatch.Index;
                        }

                        partialMatches[j] = partMatch;
                    } else {
                        partialMatches.Remove(partMatch);
                    }
                }

                if (searching == patternToFind[0]) {
                    if (patternToFind.Length == 1) {
                        return i;
                    }
                    partialMatches.Add(new PartialMatch(i));
                }
            }

            return -1;
        }
    }
}
