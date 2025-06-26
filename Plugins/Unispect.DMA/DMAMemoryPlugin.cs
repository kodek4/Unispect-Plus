﻿/*
 * DMA Memory Plugin for Unispect
 * 
 * Original Author: realbadidas (https://github.com/realbadidas)
 * Fork & Improvements: gmh5225 (https://github.com/gmh5225)
 * 
 * This plugin enables Unispect to use DMA (Direct Memory Access) via FPGA cards
 * for memory reading from a secondary computer. Based on MemProcFS/PciLeech.
 * 
 * Key improvements in this version:
 * - Fixed partial reads bug from original
 * - Proper disposal handling to prevent FPGA connection issues
 * - Enhanced error handling and logging
 * 
 * Repository: https://github.com/gmh5225/DMA-unispectDMAPlugin
 */

using System;
using System.IO;
using Unispect.SDK;
using vmmsharp;

namespace UnispectDMAPlugin
{
    [UnispectPlugin]
    public sealed class DMAMemoryPlugin : MemoryProxy
    {
        private const string MemMapPath = "mmap.txt";
        private readonly Vmm _vmm;
        private uint _pid;

        public DMAMemoryPlugin()
        {
            try
            {
                Log.Add("[DMA] Plugin Starting...");
                if (File.Exists(MemMapPath))
                {
                    Log.Add("[DMA] Memory Map Found!");
                    _vmm = new Vmm("-device", "FPGA", "-memmap", MemMapPath, "-waitinitialize");
                }
                else
                    _vmm = new Vmm("-device", "FPGA", "-waitinitialize");
                Log.Add("[DMA] Plugin Loaded!");
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException("[DMA] ERROR Initializing FPGA", ex);
            }
        }

        public override ModuleProxy GetModule(string moduleName)
        {
            try
            {
                Log.Add($"[DMA] Module Search: '{moduleName}'");
                var module = _vmm.Map_GetModuleFromName(_pid, moduleName);
                Log.Add($"[DMA] Module Found: '{module.wszText}' | Base: 0x{module.vaBase.ToString("X")} | Size: {module.cbImageSize}");
                return new ModuleProxy(moduleName, module.vaBase, (int)module.cbImageSize);
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException($"[DMA] ERROR retrieving module '{moduleName}'", ex);
            }
        }

        public override bool AttachToProcess(string handle)
        {
            try
            {
                Log.Add($"[DMA] Attaching to process '{handle}'");
                
                // Auto-append .exe if not present for better UX
                // VMM requires the full executable name with .exe extension
                string processName = handle.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) 
                    ? handle 
                    : handle + ".exe";
                    
                Log.Add($"[DMA] Looking for process: '{processName}'");
                if (!_vmm.PidGetFromName(processName, out _pid))
                    throw new Exception("Process not found!");
                return true;

            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException($"[DMA] ERROR attaching to process '{handle}'", ex);
            }
        }

        public override byte[] Read(ulong address, int length)
        {
            try
            {
                // Fixed partial reads bug from original
                return _vmm.MemRead(_pid, address, (uint)length);
            }
            catch (Exception ex)
            {
                throw new DMAMemoryPluginException($"[DMA] ERROR Reading {length} bytes at 0x{address.ToString("X")}", ex);
            }
        }

        #region IDisposable
        private bool _disposed;
        public override void Dispose()
        {
            if (!_disposed)
            {
                Log.Add("[DMA] Dispose");
                _vmm.Dispose();
                _disposed = true;
            }
        }
        #endregion
    }
}