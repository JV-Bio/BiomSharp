// BiomSharp: Copyright (c) Businessware Architects
// Licensed under the MIT License
// See: https://biomsharp.github.io/BiomSharp/LICENSE.txt

namespace BiomSharp.Imaging.Wsq
{
    public class WsqParameters
    {
        public static readonly WsqParameters Default = new();

        public WsqParameters()
        {
            NistHeader = false;
            BitRate = 0.75f;
            Resolution = -1;
            Black = 0;
            White = 255;
            Filter = WsqFilterType.Odd7x9;
            PackedDHT = false;
            Comments = Array.Empty<string>();
        }

        public bool NistHeader { get; set; } = false;

        /// <summary>
        /// The minimum supported <see cref="BitRate"/>, <c>0.1875f</c> bits per pixel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is an allowed encoder setting, not a recommended fingerprint quality threshold.
        /// <br/>Evaluations of a limited set of fingerprint images showed increasingly smoothed and
        /// distorted decompressed ridge detail as the bitrate decreased, with more pronounced
        /// degradation below <c>0.375f</c>.
        /// <br/>Suitability for biometric matching requires
        /// validation with representative images and the intended matcher.
        /// </para>
        /// <para>
        /// The nominal mapping <c>ratio = 11.25 / bitrate</c> gives a label of <c>60:1</c>
        /// at this minimum.
        /// <br/>This is not a guaranteed compression ratio; the measured ratio
        /// depends on image content and encoded file overhead.
        /// </para>
        /// </remarks>
        public const float BitRateMinValue = 0.1875f;

        /// <summary>
        /// The maximum supported <see cref="BitRate"/>, <c>2.25f</c> bits per pixel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Higher bitrates generally preserve more image detail and produce larger files,
        /// but WSQ compression remains lossy even at this maximum.
        /// </para>
        /// <para>
        /// The nominal mapping <c>ratio = 11.25 / bitrate</c> gives a label of <c>5:1</c>
        /// at this maximum, not a guaranteed compression ratio.
        /// </para>
        /// </remarks>
        public const float BitRateMaxValue = 2.25f;


        private float bitRate = 0.75f;

        /// <summary>
        /// Gets or sets the target WSQ encoding bitrate in bits per pixel.
        /// </summary>
        /// <value>
        /// The default is <c>0.75f</c>. Values below <see cref="BitRateMinValue"/> or above
        /// <see cref="BitRateMaxValue"/> are clamped to the corresponding limit.
        /// </value>
        /// <remarks>
        /// <para>
        /// Lower bitrates generally produce smaller files, but the decompressed image becomes
        /// increasingly smoothed and distorted, potentially losing fingerprint ridge and
        /// minutiae detail.
        /// <br/>A supported bitrate does not guarantee suitability for biometric
        /// matching or compliance with archival requirements.
        /// </para>
        /// <para>
        /// This is an encoder target, not the achieved file bitrate. The default corresponds
        /// to a nominal <c>15:1</c> label under <c>ratio = 11.25 / bitrate</c>;
        /// actual compression ratios vary with image content and encoded file overhead.
        /// </para>
        /// </remarks>
        public float BitRate
        {
            get => bitRate;
            set => bitRate = value > BitRateMaxValue ? BitRateMaxValue : value < BitRateMinValue ? BitRateMinValue : value;
        }

        public int Resolution { get; set; } = -1;

        public int Black { get; set; } = 0;

        public int White { get; set; } = 255;

        public WsqFilterType Filter { get; set; } = WsqFilterType.Odd7x9;

        public bool PackedDHT { get; set; } = false;

        public string[] Comments { get; set; } = Array.Empty<string>();

        public int ImplementerId { get; set; } = 0;
    }
}
