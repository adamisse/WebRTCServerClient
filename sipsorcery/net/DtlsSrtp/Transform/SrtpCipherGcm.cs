using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.IO;

namespace SIPSorcery.net.DtlsSrtp.Transform
{
    /**
     * SRTP encryption in GCM mode.
     */
    public class SrtpCipherGcm
    {
        private const int BLKLEN = 32;
        private const int MAX_BUFFER_LENGTH = 10 * 1024;
        private byte[] cipherInBlock = new byte[BLKLEN];
        private byte[] tmpCipherBlock = new byte[BLKLEN];
        private byte[] streamBuf = new byte[1024];

        public void Process(IBlockCipher cipher, MemoryStream data, int off, int len, byte[] iv)
        {
            // if data fits in inner buffer - use it. Otherwise allocate bigger
            // buffer store it to use it for later processing - up to a defined
            // maximum size.
            byte[] cipherStream = null;
            if (len > streamBuf.Length)
            {
                cipherStream = new byte[len];
                if (cipherStream.Length <= MAX_BUFFER_LENGTH)
                {
                    streamBuf = cipherStream;
                }
            }
            else
            {
                cipherStream = streamBuf;
            }

            GetCipherStream(cipher, cipherStream, len, iv);
            for (int i = 0; i < len; i++)
            {
                data.Position = i + off;
                var byteToWrite = data.ReadByte();
                data.Position = i + off;
                data.WriteByte((byte)(byteToWrite ^ cipherStream[i]));
            }
        }

        /**
         * Computes the cipher stream for AES GCM mode. See section 4.1.1 in RFC3711
         * for detailed description.
         * 
         * @param out
         *            byte array holding the output cipher stream
         * @param length
         *            length of the cipher stream to produce, in bytes
         * @param iv
         *            initialization vector used to generate this cipher stream
         */
        public void GetCipherStream(IBlockCipher aesCipher, byte[] _out, int length, byte[] iv)
        {
            System.Array.Copy(iv, 0, cipherInBlock, 0, 12);

            int ctr;
            for (ctr = 0; ctr < length / BLKLEN; ctr++)
            {
                // compute the cipher stream
                cipherInBlock[12] = (byte)((ctr & 0xFF00) >> 8);
                cipherInBlock[13] = (byte)((ctr & 0x00FF));

                aesCipher.ProcessBlock(cipherInBlock, 0, _out, ctr * BLKLEN);
            }

            // Treat the last bytes:
            cipherInBlock[12] = (byte)((ctr & 0xFF00) >> 8);
            cipherInBlock[13] = (byte)((ctr & 0x00FF));

            aesCipher.ProcessBlock(cipherInBlock, 0, tmpCipherBlock, 0);
            System.Array.Copy(tmpCipherBlock, 0, _out, ctr * BLKLEN, length % BLKLEN);
        }
    }
}
