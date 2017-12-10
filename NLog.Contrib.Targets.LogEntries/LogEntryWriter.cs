using System;
using System.Text;

namespace NLog.Contrib.Targets.LogEntries
{
    static class LogEntryWriter
    {
#if(DEBUG)
        const int _bufferLength = 64;
#else
        const int _bufferLength = 8192;
#endif

        static readonly byte[] _buffer = new byte[_bufferLength];
        static readonly char[] _charBuffer = new char[_bufferLength];
        static readonly Encoder _encoding = Encoding.UTF8.GetEncoder();
        static readonly char _newLineReplacement = '\u2028';
        static readonly byte _newLineByte = (byte)'\n';

        internal static void Write(byte[] token, string entry, LogEntriesConnection connection)
        {
            Array.Copy(token, 0, _buffer, 0, token.Length);

            var buffered = token.Length;
            var readed = 0;
            while (readed != entry.Length)
            {
                ReplaceAndBufferChars(entry, readed, _charBuffer, out int used, out int formatted);
                readed += used;

                var completed = false;
                var totalCharsUsed = 0;
                var bytesUsed = 0;

                while (!completed)
                {
                    _encoding.Convert(
                                chars: _charBuffer,
                                charIndex: totalCharsUsed,
                                charCount: formatted - totalCharsUsed,
                                bytes: _buffer,
                                byteIndex: buffered,
                                byteCount: _buffer.Length - buffered,
                                flush: formatted == totalCharsUsed,
                                charsUsed: out int charsUsed,
                                bytesUsed: out bytesUsed,
                                completed: out completed);

                    buffered += bytesUsed;
                    totalCharsUsed += charsUsed;

                    if (completed && readed == entry.Length)
                    {
                        if (buffered < _buffer.Length)
                        {
                            _buffer[buffered++] = _newLineByte;
                        }
                        else // data fits the end of the buffer so in order to
                        {    // send the end line delimitator \n it is needed to 
                             // clean the buffer. This may occur very little times.
                            connection.Send(_buffer, buffered);
                            _buffer[0] = _newLineByte;
                            buffered = 1;
                        }
                    }

                    connection.Send(_buffer, buffered);
                    buffered = 0;
                }
            }
        }

        // Buffers chars in the array and replace line breaks in the same run
        static void ReplaceAndBufferChars(string line, int offset, char[] buffer, out int lineCharsUsed, out int charsFormatted)
        {
            lineCharsUsed = 0;
            charsFormatted = 0;

            for (; offset < line.Length; offset++)
            {
                if (buffer.Length == charsFormatted)
                    break;

                if (line[offset] == '\n' || line[offset] == '\r')
                {
                    if (buffer.Length - charsFormatted < 1)
                        break;

                    buffer[charsFormatted++] = _newLineReplacement;

                    if (offset + 1 < line.Length && line[offset] == '\r' && line[offset + 1] == '\n')
                    {
                        offset++;
                        lineCharsUsed++;
                    }
                }
                else
                {
                    buffer[charsFormatted++] = line[offset];
                }

                lineCharsUsed++;
            }
        }
    }
}
