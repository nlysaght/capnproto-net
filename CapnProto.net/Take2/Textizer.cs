﻿using System;
using System.IO;
using System.Text;

namespace CapnProto.Take2
{
    class Textizer : IRecyclable, IDisposable
    {
        static Textizer Create()
        {
            return Cache<Textizer>.Pop() ?? new Textizer();
        }
        void IDisposable.Dispose()
        {
            Cache<Textizer>.Push(this);
        }
        private static readonly Encoding encoding = new UTF8Encoding(false);

        void IRecyclable.Reset(bool reusing)
        {
            if (reusing)
            {
                if (encoder != null) encoder.Reset();
                if (decoder != null) decoder.Reset();
            }
        }

        private readonly char[] chars;
        private readonly byte[] bytes;
        private Encoder encoder;
        private Decoder decoder;
        public Textizer()
        {
            chars = new char[CHAR_LENGTH];
            bytes = new byte[BYTE_LENGTH];
        }
        const int CHAR_LENGTH = 512, MAX_BYTES_TO_DECODE = CHAR_LENGTH, MAX_CHARS_TO_ENCODE = CHAR_LENGTH;
        static readonly int BYTE_LENGTH = encoding.GetMaxByteCount(CHAR_LENGTH);


        public static int AppendTo(Pointer pointer, TextWriter destination)
        {
            pointer = pointer.Dereference();
            if (!pointer.IsValid) return 0;
            int len = pointer.SingleByteLength;
            if (--len <= 0 || pointer.GetByte(len) != 0) return 0;
            using (var text = Create())
            {
                var decoder = text.decoder ?? (text.decoder = encoding.GetDecoder());
                byte[] bytes = text.bytes;
                char[] chars = text.chars;
                int wordOffset = 0, totalChars = 0;
                do
                {
                    int bytesThisPass = Math.Min(len, MAX_BYTES_TO_DECODE);
                    int wordsThisPass = ((bytesThisPass - 1) >> 3) + 1;
                    pointer.ReadWords(wordOffset, bytes, 0, wordsThisPass);
                    int charCount = decoder.GetChars(bytes, 0, bytesThisPass, chars, 0, false);
                    totalChars += charCount;
                    wordOffset += wordsThisPass;
                    len -= bytesThisPass;
                    if (charCount != 0) destination.Write(chars, 0, charCount);
                } while (len > 0);
                return totalChars;
            }
        }
        public static int Count(Pointer pointer)
        {
            pointer = pointer.Dereference();
            if (!pointer.IsValid) return 0;
            int len = pointer.SingleByteLength;
            if (--len <= 0 || pointer.GetByte(len) != 0) return 0;
            using (var text = Create())
            {
                var decoder = text.decoder ?? (text.decoder = encoding.GetDecoder());
                byte[] bytes = text.bytes;
                int wordOffset = 0, totalChars = 0;
                do
                {
                    int bytesThisPass = Math.Min(len, MAX_BYTES_TO_DECODE);
                    int wordsThisPass = ((bytesThisPass - 1) >> 3) + 1;
                    pointer.ReadWords(wordOffset, bytes, 0, wordsThisPass);
                    int charCount = decoder.GetCharCount(bytes, 0, bytesThisPass, false);
                    totalChars += charCount;
                    wordOffset += wordsThisPass;
                    len -= bytesThisPass;
                } while (len > 0);
                return totalChars;
            }
        }
        public static int AppendTo(Pointer pointer, StringBuilder destination)
        {
            pointer = pointer.Dereference();
            if (!pointer.IsValid) return 0;
            int len = pointer.SingleByteLength;
            if (--len <= 0 || pointer.GetByte(len) != 0) return 0;
            using (var text = Create())
            {
                var decoder = text.decoder ?? (text.decoder = encoding.GetDecoder());
                byte[] bytes = text.bytes;
                char[] chars = text.chars;
                int wordOffset = 0, totalChars = 0;
                do
                {
                    int bytesThisPass = Math.Min(len, MAX_BYTES_TO_DECODE);
                    int wordsThisPass = ((bytesThisPass - 1) >> 3) + 1;
                    pointer.ReadWords(wordOffset, bytes, 0, wordsThisPass);
                    int charCount = decoder.GetChars(bytes, 0, bytesThisPass, chars, 0, false);
                    totalChars += charCount;
                    wordOffset += wordsThisPass;
                    len -= bytesThisPass;
                    if (charCount != 0) destination.Append(chars, 0, charCount);
                } while (len > 0);
                return totalChars;
            }
        }

        internal static void Write(Pointer pointer, char[] value, int offset, int count)
        {
            pointer = pointer.Dereference();
            if (!pointer.IsValid) throw new InvalidOperationException();
            int len = pointer.SingleByteLength;
            if(--len == 0) throw new InvalidOperationException();
            if (len == 0)
            {   // empty string
                //pointer.SetByte(len, 0);
                pointer.SetDataWord(len, 0, 0xFF);
                return;
            }

            
            using (var text = Create())
            {
                var encoder = text.encoder ?? (text.encoder = encoding.GetEncoder());

                    
                byte[] bytes = text.bytes;
                int byteOffset = 0, wordOffset = 0, totalBytes = 0;
                do
                {
                    int charsThisPass = Math.Min(count, MAX_CHARS_TO_ENCODE);
                    int newBytes = encoder.GetBytes(value, offset, charsThisPass, bytes, byteOffset, false);

                    // note: we could have some complete words, and some partial words; need to be careful

                    int availableBytes = newBytes + byteOffset;
                    int fullWords = availableBytes >> 3;
                    if(fullWords != 0) pointer.WriteWords(wordOffset, bytes, 0, fullWords);
                    totalBytes += newBytes;
                    wordOffset += fullWords;
                    byteOffset = availableBytes & 7;
                    if(byteOffset != 0)
                    {
                        // copy down those spare bytes
                        Buffer.BlockCopy(bytes, fullWords << 3, bytes, 0, byteOffset);
                    }
                    offset += charsThisPass;
                    count -= charsThisPass;
                } while (count > 0);

                // copy in any spare trailing bytes
                int origin = wordOffset << 3;
                for (int i = 0; i < byteOffset;i++ )
                {
                    pointer.SetDataWord(origin++, bytes[i], 0xFF);
                    //pointer.SetByte(origin++, bytes[i]);
                }
                pointer.SetDataWord(len, 0, 0xFF); // add the nul-terminator
                //pointer.SetByte(len, 0); 

                if (totalBytes != len) throw new InvalidOperationException("String encoded length mismatch");
            }
            
        }
    }

}