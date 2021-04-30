﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Stride.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU
{
    /// <summary>
    /// A generic stride description based on the given <typeparamref name="TIndex"/>
    /// type.
    /// </summary>
    /// <typeparam name="TIndex">The underlying n-D index type.</typeparam>
    public interface IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
    {
        /// <summary>
        /// Returns the associated stride extent.
        /// </summary>
        TIndex StrideExtent { get; }
    }

    /// <summary>
    /// A generic stride based on 32-bit and 64-bit index information.
    /// </summary>
    /// <typeparam name="TIndex">The actual 32-bit stride index.</typeparam>
    /// <typeparam name="TLongIndex">The 64-bit stride index.</typeparam>
    public interface IStride<TIndex, TLongIndex> : IStride<TIndex>
        where TIndex : struct, IIntIndex<TIndex, TLongIndex>
        where TLongIndex : struct, ILongIndex<TLongIndex, TIndex>
    {
        /// <summary>
        /// Computes the linear 32-bit element address using the given index.
        /// </summary>
        /// <param name="index">The dimension for index computation.</param>
        /// <returns>The computed linear element address.</returns>
        int ComputeElementIndex(TIndex index);

        /// <summary>
        /// Computes the linear 64-bit element address using the given index.
        /// </summary>
        /// <param name="index">The dimension for index computation.</param>
        /// <returns>The computed linear element address.</returns>
        long ComputeElementIndex(TLongIndex index);

        /// <summary>
        /// Computes the 64-bit length of a required allocation.
        /// </summary>
        /// <param name="extent">The extent to allocate.</param>
        /// <returns>The 64-bit length of a required allocation.</returns>
        long ComputeBufferLength(TLongIndex extent);
    }

    /// <summary>
    /// Contains helper functions for generic <see cref="IStride{TIndex}"/> types.
    /// </summary>
    public static class StrideExtensions
    {
        /// <summary>
        /// Determines a pitched leading dimension.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="leadingDimension">The size of the leading dimension.</param>
        /// <param name="alignmentInBytes">
        /// The alignment in bytes of the leading dimension.
        /// </param>
        /// <returns>The pitched leading dimension.</returns>
        public static long GetPitchedLeadingDimension<T>(
            long leadingDimension,
            int alignmentInBytes)
            where T : unmanaged
        {
            // Validate the byte pitch and the element size
            if (alignmentInBytes < 1 || !Utilities.IsPowerOf2(alignmentInBytes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alignmentInBytes));
            }
            int elementSize = ArrayView<T>.ElementSize;
            if (elementSize > alignmentInBytes || alignmentInBytes % elementSize != 0)
            {
                throw new ArgumentException(
                    string.Format(
                        RuntimeErrorMessages.NotSupportedPitchedAllocation,
                        nameof(T),
                        alignmentInBytes));
            }

            // Ensure a proper alignment of the leading dimension
            long unpichtedBytes = leadingDimension * elementSize;
            long pitchedBytes = TypeNode.Align(unpichtedBytes, alignmentInBytes);

            // Return the pitched dimension
            return pitchedBytes / elementSize;
        }
    }

    partial class Stride2D
    {
        /// <summary>
        /// A 2D dense X stride.
        /// </summary>
        public readonly struct DenseX : IStride2D
        {
            #region Instance

            /// <summary>
            /// Constructs a new dense X stride.
            /// </summary>
            /// <param name="yStride">The stride of the Y dimension.</param>
            public DenseX(int yStride)
            {
                Trace.Assert(yStride >= 1, "yStride out of range");

                YStride = yStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int XStride => 1;

            /// <summary>
            /// Returns the Y-dimension stride.
            /// </summary>
            public int YStride { get; }

            /// <summary>
            /// Returns the associated stride extent of the form (1, YStride).
            /// </summary>
            public readonly Index2D StrideExtent => new Index2D(XStride, YStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex2D extent)
            {
                Trace.Assert(extent.X <= YStride, "X extent out of range");
                return YStride * extent.Y;
            }

            /// <summary>
            /// Returns this stride as general 2D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }

        /// <summary>
        /// A 2D dense Y stride.
        /// </summary>
        public readonly struct DenseY : IStride2D
        {
            #region Instance

            /// <summary>
            /// Constructs a new dense Y stride.
            /// </summary>
            /// <param name="xStride">The stride of the X dimension.</param>
            public DenseY(int xStride)
            {
                Trace.Assert(xStride >= 1, "xStride out of range");

                XStride = xStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the X-dimension stride.
            /// </summary>
            public int XStride { get; }

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int YStride => 1;

            /// <summary>
            /// Returns the associated stride extent of the form (XStride, 1).
            /// </summary>
            public readonly Index2D StrideExtent => new Index2D(XStride, YStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex2D index) =>
                Stride2D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex2D extent)
            {
                Trace.Assert(extent.Y <= XStride, "Y extent out of range");
                return XStride * extent.X;
            }

            /// <summary>
            /// Returns this stride as general 2D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }
    }

    partial class Stride3D
    {
        /// <summary>
        /// A 3D dense XY stride.
        /// </summary>
        public readonly struct DenseXY : IStride3D
        {
            #region Instance

            /// <summary>
            /// Constructs a new dense XY stride.
            /// </summary>
            /// <param name="yStride">The stride of the Y dimension.</param>
            /// <param name="zStride">The stride of the Z dimension.</param>
            public DenseXY(int yStride, int zStride)
            {
                Trace.Assert(yStride >= 1, "yStride out of range");
                Trace.Assert(zStride >= 1, "zStride out of range");
                Trace.Assert(zStride >= yStride, "zStride out of range");

                YStride = yStride;
                ZStride = zStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int XStride => 1;

            /// <summary>
            /// Returns the Y-dimension stride.
            /// </summary>
            public int YStride { get; }

            /// <summary>
            /// Returns the Z-dimension stride.
            /// </summary>
            public int ZStride { get; }

            /// <summary>
            /// Returns the associated stride extent of the form (1, YStride, ZStride).
            /// </summary>
            public readonly Index3D StrideExtent =>
                new Index3D(XStride, YStride, ZStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex3D extent)
            {
                Trace.Assert(extent.X <= YStride, "X extent out of range");
                Trace.Assert(extent.X * extent.Y <= ZStride, "Y extent out of range");
                return ZStride * extent.Z;
            }

            /// <summary>
            /// Returns this stride as general 3D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }

        /// <summary>
        /// A 3D dense ZY stride.
        /// </summary>
        public readonly struct DenseZY : IStride3D
        {
            #region Instance

            /// <summary>
            /// Constructs a new dense ZY stride.
            /// </summary>
            /// <param name="xStride">The stride of the X dimension.</param>
            /// <param name="yStride">The stride of the Y dimension.</param>
            public DenseZY(int xStride, int yStride)
            {
                Trace.Assert(xStride >= 1, "xStride out of range");
                Trace.Assert(yStride >= 1, "yStride out of range");
                Trace.Assert(xStride >= yStride, "zStride out of range");

                XStride = xStride;
                YStride = yStride;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the X-dimension stride.
            /// </summary>
            public int XStride { get; }

            /// <summary>
            /// Returns the Y-dimension stride.
            /// </summary>
            public int YStride { get; }

            /// <summary>
            /// Returns the constant 1.
            /// </summary>
            public readonly int ZStride => 1;

            /// <summary>
            /// Returns the associated stride extent of the form (XStride, YStride, 1).
            /// </summary>
            public readonly Index3D StrideExtent =>
                new Index3D(XStride, YStride, ZStride);

            #endregion

            #region Methods

            /// <summary>
            /// Computes the linear 32-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int ComputeElementIndex(Index3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the linear 64-bit element address using the given index.
            /// </summary>
            /// <param name="index">The dimension for index computation.</param>
            /// <returns>The computed linear element address.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeElementIndex(LongIndex3D index) =>
                Stride3D.ComputeElementIndex(this, index);

            /// <summary>
            /// Computes the 64-bit length of a required allocation.
            /// </summary>
            /// <param name="extent">The extent to allocate.</param>
            /// <returns>The 64-bit length of a required allocation.</returns>
            /// <remarks>This method is not supported on accelerators.</remarks>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly long ComputeBufferLength(LongIndex3D extent)
            {
                Trace.Assert(extent.Z <= YStride, "Y extent out of range");
                Trace.Assert(extent.Z * extent.Y <= XStride, "Z extent out of range");
                return XStride * extent.X;
            }

            /// <summary>
            /// Returns this stride as general 3D stride.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly General AsGeneral() => new General(StrideExtent);

            #endregion

            #region Object

            /// <summary>
            /// Returns the string representation of this stride.
            /// </summary>
            /// <returns>The string representation of this stride.</returns>
            [NotInsideKernel]
            public readonly override string ToString() => StrideExtent.ToString();

            #endregion
        }
    }
}