# Python Script to Generate Bulk CNBV Fixtures with Low System Load

import subprocess
import random
import time
import math
import os

# --- Configuration ---
TOTAL_DOCUMENTS = 500
BATCH_SIZE = 10
DELAY_BETWEEN_BATCHES = 10  # Seconds
OUTPUT_DIRECTORY = "bulk_generated_documents_all_formats"
GENERATOR_SCRIPT = "Prisma/Fixtures/generators/AAAV2_refactored/main_generator.py"

# --- Generation Parameters (for diversity) ---
CHAOS_LEVELS = ["none", "low", "medium", "high"]
REQUIREMENT_TYPES = ["fiscal", "judicial", "pld", "aseguramiento", "informacion"]
AUTHORITIES = ["IMSS", "SAT", "UIF", "FGR", "SEIDO", "PJF", "INFONAVIT", "SHCP", "CONDUSEF"]
FORMATS = ["html", "pdf", "docx", "xml"]

def main():
    """Main function to orchestrate the document generation."""
    number_of_batches = math.ceil(TOTAL_DOCUMENTS / BATCH_SIZE)

    print(f"🚀 Starting bulk generation of {TOTAL_DOCUMENTS} documents in {number_of_batches} batches.")
    print("----------------------------------------------------------------")

    # Create output directory if it doesn't exist
    if not os.path.exists(OUTPUT_DIRECTORY):
        os.makedirs(OUTPUT_DIRECTORY)

    for i in range(1, number_of_batches + 1):
        # --- Randomize parameters for this batch ---
        random_chaos = random.choice(CHAOS_LEVELS)
        random_type = random.choice(REQUIREMENT_TYPES)
        random_authority = random.choice(AUTHORITIES)

        print(f"🔥 Batch {i} / {number_of_batches}: Generating {BATCH_SIZE} documents...")
        print(f"   - Chaos Level: {random_chaos}")
        print(f"   - Requirement Type: {random_type}")
        print(f"   - Authority: {random_authority}")
        print(f"   - Formats: {', '.join(FORMATS)}")

        # --- Construct the command ---
        command = [
            "python", GENERATOR_SCRIPT,
            "--count", str(BATCH_SIZE),
            "--output", OUTPUT_DIRECTORY,
            "--chaos", random_chaos,
            "--types", random_type,
            "--authority", random_authority,
            "--formats", *FORMATS
        ]
        
        try:
            # Using subprocess.run to execute the command
            process = subprocess.run(command, check=True, capture_output=True, text=True)
            print(f"✅ Batch {i} completed successfully.")
            # You can print process.stdout if you want to see the generator's output
        except subprocess.CalledProcessError as e:
            print(f"❌ Error during batch {i}. See details below:")
            print(f"   - Return Code: {e.returncode}")
            print(f"   - Stdout: {e.stdout}")
            print(f"   - Stderr: {e.stderr}")
        except FileNotFoundError:
            print(f"❌ Error: Could not find the '{GENERATOR_SCRIPT}'. Make sure the path is correct.")
            break # Exit the loop if the script is not found


        # --- Pause between batches to reduce system load ---
        if i < number_of_batches:
            print(f"💤 Pausing for {DELAY_BETWEEN_BATCHES} seconds to reduce system load...")
            time.sleep(DELAY_BETWEEN_BATCHES)
        
        print("----------------------------------------------------------------")

    print(f"🎉 Bulk generation complete. All documents are in the '{OUTPUT_DIRECTORY}' folder.")

if __name__ == "__main__":
    main()
