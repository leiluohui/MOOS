﻿using Kernel.Driver;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Kernel
{
    public static unsafe class SMP
    {
        public const ulong NumActivedProcessors = 0x80000;
        public const ulong APMain = 0x80008;
        public const ulong Stacks = 0x800016;
        public const ulong SharedGDT = 0x800024;
        public const ulong SharedIDT = 0x800032;
        public const ulong SharedPageTable = 0x81000;
        public const ulong Trampoline = 0x90000;

        public static Queue<ulong> WorkGroups;

        public static void RunOnAnyCPU(delegate*<void> method) => WorkGroups.Enqueue((ulong)method);

        public static delegate*<void> Take() 
        {
            while (WorkGroups.Length == 0) ;
            return (delegate*<void>)WorkGroups.Dequeue(); 
        }

        public static uint ThisCPU => LocalAPIC.GetId();

        public static void Initialize(uint trampoline)
        {
            ushort* activedProcessor = (ushort*)NumActivedProcessors;
            *activedProcessor = 1;

            ulong* apMain = (ulong*)APMain;
            *apMain = (ulong)(delegate*<int,void>)&Program.APMain;

            ulong* stacks = (ulong*)Stacks;
            *stacks = (ulong)Allocator.Allocate((ulong)(ACPI.LocalAPIC_CPUIDs.Count * 1048576));

            fixed(GDT.GDTDescriptor* gdt = &GDT.gdtr) 
            {
                ulong* sgdt = (ulong*)SharedGDT;
                *sgdt = (ulong)gdt;
            }

            fixed (IDT.IDTDescriptor* idt = &IDT.idtr)
            {
                ulong* sidt = (ulong*)SharedIDT;
                *sidt = (ulong)idt;
            }

            WorkGroups = new Queue<ulong>();

            int NumCPU = ACPI.LocalAPIC_CPUIDs.Count;
            for (int i = 0; i < NumCPU; ++i)
            {
                uint id = ACPI.LocalAPIC_CPUIDs[i]; 
                Console.WriteLine($"Starting CPU{id.ToString()}");
                if (id != ThisCPU)
                {
                    LocalAPIC.SendInit(id); 
                    LocalAPIC.SendStartup(id, (trampoline >> 12));
                }
            }
            while (*activedProcessor != NumCPU);
            Console.WriteLine("All CPUs started");
        }
    }
}