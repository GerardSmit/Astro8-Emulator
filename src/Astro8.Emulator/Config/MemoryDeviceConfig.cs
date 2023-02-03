﻿using Astro8.Devices;

namespace Astro8;

public class MemoryDeviceConfig
{
    public Address Screen { get; set; } = new(1, 0xD26F);

    public Address Character { get; set; } = new(1, 0xD12A);

    public Address Program { get; set; } = new(0, 0x0000);

    public Address Keyboard { get; set; } = new(1, 0xD0FC);

    public Address Mouse { get; set; } = new(1, 0xD0FD);
}
