# Python Script for a Small Test Batch Generation

import subprocess
import random
import time
import os

# --- Configuration for the Test Run ---
NUM_BATCHES = 3
BATCH_SIZE = 2
DELAY_BETWEEN_BATCHES = 5  # Seconds
OUTPUT_DIRECTORY = "test_batch_output"
GENERATOR_SCRIPT = "Prisma/Fixtures/generators/AAAV2_refactored/main_generator.py"

# --- Generation Parameters ---
CHAOS_LEVELS = ["none", "low", "medium", "high"]
REQUIREMENT_TYPES = ["fiscal", "judicial", "pld", "aseguramiento", "informacion"]
AUTHORITIES = ["IMSS", "SAT", "UIF", "FGR", "SEIDO", "PJF", "INFONAVIT", "SHCP", "CONDUSEF"]
# Generate all formats for this test
FORMATS = ["html", "pdf", "docx", "xml"]

def main():
    """Main function to orchestrate the test batch generation."""
    total_docs = NUM_BATCHES * BATCH_SIZE
    print(f"🚀 Starting TEST RUN: Generating {total_docs} documents in {NUM_BATCHES} batches of {BATCH_SIZE}.")
    print(f"   Outputting all formats: {', '.join(FORMATS)}")
    print("----------------------------------------------------------------")

    # Create output directory if it doesn't exist
    if not os.path.exists(OUTPUT_DIRECTORY):
        os.makedirs(OUTPUT_DIRECTORY)

    for i in range(1, NUM_BATCHES + 1):
        # --- Randomize parameters for this batch ---
        random_chaos = random.choice(CHAOS_LEVELS)
        random_type = random.choice(REQUIREMENT_TYPES)
        random_authority = random.choice(AUTHORITIES)

        print(f"🔥 Test Batch {i} / {NUM_BATCHES}: Generating {BATCH_SIZE} documents...")
        print(f"   - Chaos Level: {random_chaos}")
        print(f"   - Requirement Type: {random_type}")
        print(f"   - Authority: {random_authority}")

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
            print(f"✅ Test Batch {i} completed successfully.")
        except subprocess.CalledProcessError as e:
            print(f"❌ Error during Test Batch {i}. See details below:")
            print(f"   - Return Code: {e.returncode}")
            print(f"   - Stdout: {e.stdout}")
            print(f"   - Stderr: {e.stderr}")
            break # Stop on error during test run
        except FileNotFoundError:
            print(f"❌ Error: Could not find the '{GENERATOR_SCRIPT}'. Make sure the path is correct.")
            break

        # --- Pause between batches ---
        if i < NUM_BATCHES:
            print(f"💤 Pausing for {DELAY_BETWEEN_BATCHES} seconds...")
            time.sleep(DELAY_BETWEEN_BATCHES)
        
        print("----------------------------------------------------------------")

    print(f"🎉 Test run complete. Please check the '{OUTPUT_DIRECTORY}' folder.")

if __name__ == "__main__":
    main()
