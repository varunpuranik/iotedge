// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage.RocksDb.Disk
{
    public enum DiskSpaceStatus
    {
        Unknown = 0,
        Available,
        Critical,
        Full
    }
}