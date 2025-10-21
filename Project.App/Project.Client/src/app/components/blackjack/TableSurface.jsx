"use client";
import React from "react";

export default function TableSurface({ children }) {
  return (
    <div className="relative w-[900px] h-[500px] flex items-center justify-center mt-10">
      <div className="absolute inset-0 bg-green-700 rounded-[50%/35%] border-[6px] border-yellow-800"></div>
      <div
        className="absolute top-[-160px] left-1/2 -translate-x-1/2 w-[420px] h-[300px] rounded-b-full overflow-hidden bg-black"
        style={{ boxShadow: "none" }}
      ></div>
      {children}
    </div>
  );
}
