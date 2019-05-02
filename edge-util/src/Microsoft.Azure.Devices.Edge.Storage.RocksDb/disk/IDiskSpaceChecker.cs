// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    public interface IDiskSpaceChecker
    {
        void SetThresholdPercentage(int thresholdPercentage);

        void SetMaxDiskUsageSize(long maxDiskUsageBytes);

        bool IsFull { get; }
    }
}
